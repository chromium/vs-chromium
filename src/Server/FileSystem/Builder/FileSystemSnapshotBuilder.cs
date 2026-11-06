// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem.Builder {
  public class FileSystemSnapshotBuilder : IFileSystemSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly IIndexingWorkerPool _indexingWorkerPool;
    private readonly IChangeJournal _changeJournal;

    public FileSystemSnapshotBuilder(
      IFileSystem fileSystem,
      IProgressTrackerFactory progressTrackerFactory,
      IIndexingWorkerPool indexingWorkerPool,
      IChangeJournal changeJournal = null) {
      _fileSystem = fileSystem;
      _progressTrackerFactory = progressTrackerFactory;
      _indexingWorkerPool = indexingWorkerPool;
      _changeJournal = changeJournal;
    }

    public FileSystemSnapshot Compute(IFileSystemNameFactory fileNameFactory,
                                          FileSystemSnapshot oldSnapshot,
                                          FullPathChanges pathChanges/* may be null */,
                                          IList<IProject> projects,
                                          int version,
                                          CancellationToken cancellationToken,
                                          SnapshotComputeOptions options = null) {
      cancellationToken.ThrowIfCancellationRequested(); // cancellation
      var computeStart = Stopwatch.GetTimestamp();
      using (OperationTimings.Measure(OperationTimings.FileSystemScan))
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        try {
          var projectRoots =
            projects
              .Distinct(new ProjectPathComparer())
              .Select(project => {
                cancellationToken.ThrowIfCancellationRequested(); // cancellation

                // The tree of the index cache (phase 2), not scanned.
                if (options?.PrecomputedRoots != null && options.PrecomputedRoots.TryGetValue(project.RootPath, out var precomputed)) {
                  ProjectOperations.RecordCachedTree(project.RootPath, computeStart);
                  return new ProjectRootSnapshot(project, precomputed.Root, new ChangeJournalCheckpointHolder(precomputed.Checkpoint));
                }

                var oldRoot = oldSnapshot?.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(project.RootPath));
                var projectSnapshotBuilder = new Func<ProjectPathChanges, ProjectRootSnapshotBuilder>(changes => new ProjectRootSnapshotBuilder(
                  _fileSystem, fileNameFactory, oldSnapshot, project, progress, changes, _indexingWorkerPool, cancellationToken));

                // Verification of the tree of the index cache: catch-up from the change journal if possible.
                string fallbackReason = null;
                if (options?.VerifyRoots != null && options.VerifyRoots.TryGetValue(project.RootPath, out var cachedCheckpoint)) {
                  var verification = new ProjectVerification();
                  options.Verifications[project.RootPath] = verification;
                  var start = Stopwatch.GetTimestamp();
                  var caughtUp = TryCatchUp(project, oldRoot, cachedCheckpoint, projectSnapshotBuilder(null), verification,
                    cancellationToken);
                  if (caughtUp != null) {
                    verification.Method = VerificationMethod.ChangeJournal;
                    verification.ElapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                    // The "scan" of the initial load from the cache.
                    ProjectOperations.RecordFullScan(project.RootPath, start, verification.ElapsedMs);
                    return caughtUp;
                  }
                  fallbackReason = verification.FallbackReason;
                }

                // Note: A null path changes means a full scan of the project.
                var fullScan = pathChanges == null || (options?.FullScanRoots != null && options.FullScanRoots.Contains(project.RootPath)) ||
                               fallbackReason != null;
                // A new project is scanned entirely too.
                var newProject = oldRoot == null;
                if (!fullScan && !newProject) {
                  // Incremental update: the tree contains the changes before the checkpoint of the old tree.
                  var rootSnapshot = projectSnapshotBuilder(new ProjectPathChanges(project.RootPath, pathChanges.Entries)).Build();
                  return new ProjectRootSnapshot(project, rootSnapshot, oldRoot.CheckpointHolder);
                }

                // The checkpoint of a scan: the end of the journal before the scan (the changes made
                // during the scan are after it: listed again by the next catch-up, never missed).
                var checkpoint = QueryCheckpoint(project.RootPath);
                var scanStart = Stopwatch.GetTimestamp();
                var scannedRoot = projectSnapshotBuilder(null).Build();
                var scanMs = Stopwatch.GetElapsedTime(scanStart).TotalMilliseconds;
                // The scan of a project, for its initial load statistics.
                ProjectOperations.RecordFullScan(project.RootPath, scanStart, scanMs);
                if (options != null && options.Verifications.TryGetValue(project.RootPath, out var fullScanVerification)) {
                  fullScanVerification.Method = VerificationMethod.FullScan;
                  fullScanVerification.ElapsedMs = scanMs;
                  fullScanVerification.CheckpointNeedsSave = checkpoint != null;
                }
                return new ProjectRootSnapshot(project, scannedRoot, new ChangeJournalCheckpointHolder(checkpoint));
              })
              .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
              .ToReadOnlyCollection();

          return new FileSystemSnapshot(version, projectRoots);
        }
        finally {
          // The intern tables share the strings of the names created during a build (3.3M names
          // but 579k distinct strings on a 2.6M file tree): they are only useful during the build,
          // and cost ~25 bytes per distinct name afterwards (15 MB on the same tree). Names
          // created later (e.g. new files) don't share their strings with the existing names,
          // which only costs a few bytes per new file (names are compared by value).
          fileNameFactory.ClearInternedStrings();
        }
      }
    }

    /// <summary>The end of the change journal of the volume of a project (before a scan), or null.</summary>
    private ChangeJournalCheckpoint QueryCheckpoint(FullPath rootPath) {
      if (_changeJournal == null)
        return null;
      var result = _changeJournal.Query(rootPath);
      if (!result.IsSuccess) {
        Logger.LogInfo("No change journal checkpoint for \"{0}\": {1}.", rootPath, result.FailureReason);
        return null;
      }
      return result.Next;
    }

    /// <summary>
    /// The catch-up of the tree of the index cache of a project (<paramref name="oldRoot"/>, written
    /// with <paramref name="checkpoint"/>) from the change journal: the directories with records
    /// since the checkpoint are listed again (<see cref="ProjectRootSnapshotBuilder.CatchUp"/>).
    /// Returns null if the journal can't be used (<see cref="ProjectVerification.FallbackReason"/>:
    /// a full scan is needed).
    /// </summary>
    private ProjectRootSnapshot TryCatchUp(IProject project, ProjectRootSnapshot oldRoot, ChangeJournalCheckpoint checkpoint,
                                           ProjectRootSnapshotBuilder builder, ProjectVerification verification,
                                           CancellationToken cancellationToken) {
      void Fail(string reason) {
        verification.FallbackReason = reason;
      }

      if (_changeJournal == null) {
        Fail("no change journal on this platform");
        return null;
      }
      if (oldRoot == null) {
        Fail("no tree to verify");
        return null;
      }
      if (checkpoint == null) {
        Fail("the index cache has no change journal checkpoint");
        return null;
      }
      if (oldRoot.Directory.FileId == UInt128.Zero) {
        Fail("the index cache has no directory ids");
        return null;
      }
      var query = _changeJournal.Query(project.RootPath);
      var reason = query.CheckReadable(checkpoint);
      if (reason != null) {
        Fail(reason);
        return null;
      }

      // Note: The end of the journal is read before the directories are listed again: the new tree
      // contains all the changes before it (its checkpoint).
      var end = query.Next;
      var mapper = new ChangeJournalDirectoryMapper(oldRoot.Directory);
      try {
        _changeJournal.ReadRecords(project.RootPath, checkpoint, end.Usn, mapper.Add, cancellationToken);
      }
      catch (ChangeJournalException e) {
        Fail(e.Message);
        return null;
      }
      verification.RecordCount = mapper.RecordCount;
      verification.CheckpointNeedsSave = end.Usn - checkpoint.Usn > (end.Usn - query.FirstUsn) / 2;

      var statistics = new CatchUpStatistics();
      var root = builder.CatchUp(oldRoot.Directory, mapper.ChangedDirectories, statistics);
      verification.ListedDirectoryCount = statistics.ListedDirectoryCount;
      verification.ScannedSubtreeCount = statistics.ScannedSubtreeCount;
      verification.SymLinkSubtreeCount = statistics.SymLinkSubtreeCount;
      verification.SymLinkFileCount = statistics.SymLinkFileCount;
      return new ProjectRootSnapshot(project, root, new ChangeJournalCheckpointHolder(end));
    }
  }
}
