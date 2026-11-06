// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  /// <summary>
  /// The journal self-check of a save of the index cache: the checkpoint saved with the tree of a
  /// project must be a position of the change journal such that every change before it is in the
  /// saved tree. The directory watcher can deliver its events late (or miss some), so instead of
  /// relying on it, before each save:
  /// <list type="number">
  /// <item>U' = the current end of the journal;</item>
  /// <item>the records from the current checkpoint to U' give the directories to check;</item>
  /// <item>these directories are listed again and compared with the saved tree (and the metadata of
  /// the saved contents);</item>
  /// <item>no difference: the tree is saved with U' (and the checkpoint of the tree in memory
  /// advances to U'); differences (missed or late watcher events): they are reported as changes
  /// (applied by the incremental update), and the tree is saved with its current checkpoint
  /// (listing a directory twice at the next start is always safe).</item>
  /// </list>
  /// The changes of the targets of symbolic links are not checked (always checked again by the
  /// catch-up), nor the directories without id (always listed again by the catch-up).
  /// </summary>
  public sealed class ChangeJournalSelfCheck {
    private readonly IChangeJournal _changeJournal;
    private readonly IFileSystem _fileSystem;
    private readonly Action<IList<PathChangeEntry>> _reportChanges;

    public ChangeJournalSelfCheck(IChangeJournal changeJournal, IFileSystem fileSystem, Action<IList<PathChangeEntry>> reportChanges) {
      _changeJournal = changeJournal;
      _fileSystem = fileSystem;
      _reportChanges = reportChanges;
    }

    /// <summary>The result of the last check (tests, diagnostics).</summary>
    public ChangeJournalSelfCheckResult LastResult { get; private set; }

    /// <summary>
    /// The checkpoint to save with the tree of <paramref name="projectRoot"/>: <paramref
    /// name="savedMetadata"/> is the metadata of each file as saved (the metadata of the read of
    /// its contents, or of the scan).
    /// </summary>
    public ChangeJournalCheckpoint Run(ProjectRootSnapshot projectRoot, Func<FileName, FileMetadata, FileMetadata> savedMetadata,
                                       CancellationToken cancellationToken) {
      var result = Check(projectRoot, savedMetadata, cancellationToken);
      LastResult = result;
      if (result.Changes.Count > 0) {
        Logger.LogInfo("Change journal self-check of \"{0}\": {1:n0} differences in {2:n0} directories listed again " +
                       "(missed changes, applied now), first: {3}; checkpoint kept.",
          projectRoot.Project.RootPath, result.Changes.Count, result.ListedDirectoryCount, result.Changes[0]);
        _reportChanges?.Invoke(result.Changes);
      } else if (result.FailureReason != null) {
        Logger.LogInfo("Change journal self-check of \"{0}\": {1}; checkpoint kept.", projectRoot.Project.RootPath, result.FailureReason);
      } else if (Logger.IsInfoEnabled) {
        Logger.LogInfo("Change journal self-check of \"{0}\": {1:n0} records, {2:n0} directories listed again, no difference: " +
                       "checkpoint {3}.", projectRoot.Project.RootPath, result.RecordCount, result.ListedDirectoryCount, result.Checkpoint);
      }
      return result.Checkpoint;
    }

    private ChangeJournalSelfCheckResult Check(ProjectRootSnapshot projectRoot, Func<FileName, FileMetadata, FileMetadata> savedMetadata,
                                               CancellationToken cancellationToken) {
      var holder = projectRoot.CheckpointHolder;
      var current = holder?.Value;
      var result = new ChangeJournalSelfCheckResult { Checkpoint = current };
      if (current == null) {
        result.FailureReason = "no checkpoint";
        return result;
      }
      var rootPath = projectRoot.Project.RootPath;
      var query = _changeJournal.Query(rootPath);
      var reason = query.CheckReadable(current);
      if (reason != null) {
        result.FailureReason = reason;
        return result;
      }
      var end = query.Next;
      if (end.Usn == current.Usn)
        return result;

      var mapper = new ChangeJournalDirectoryMapper(projectRoot.Directory);
      try {
        _changeJournal.ReadRecords(rootPath, current, end.Usn, mapper.Add, cancellationToken);
      }
      catch (ChangeJournalException e) {
        result.FailureReason = e.Message;
        return result;
      }
      result.RecordCount = mapper.RecordCount;

      // The directory symbolic links whose target changed (e.g. retargeted): scanned again.
      mapper.CheckSymLinks(_fileSystem, rootPath, end.VolumeSerialNumber);
      foreach (var symLink in mapper.SymLinksToScan) {
        result.Changes.Add(new PathChangeEntry(rootPath, symLink.DirectoryName.RelativePath, PathChangeKind.Deleted, PathKind.Directory));
        result.Changes.Add(new PathChangeEntry(rootPath, symLink.DirectoryName.RelativePath, PathChangeKind.Created, PathKind.Directory));
      }
      foreach (var directory in mapper.ChangedDirectories) {
        cancellationToken.ThrowIfCancellationRequested();
        result.ListedDirectoryCount++;
        CompareDirectory(projectRoot, directory, savedMetadata, end.VolumeSerialNumber, result.Changes);
      }
      if (result.Changes.Count == 0) {
        holder.Advance(end);
        result.Checkpoint = end;
      }
      return result;
    }

    /// <summary>Adds the differences between a directory of the tree and its listing.</summary>
    private void CompareDirectory(ProjectRootSnapshot projectRoot, DirectorySnapshot directory,
                                  Func<FileName, FileMetadata, FileMetadata> savedMetadata, ulong projectVolume,
                                  List<PathChangeEntry> changes) {
      var project = projectRoot.Project;
      var rootPath = project.RootPath;
      var relativePath = directory.DirectoryName.RelativePath;
      IList<DirectoryEntry> entries;
      try {
        entries = _fileSystem.GetDirectoryEntries(rootPath.Combine(relativePath));
      }
      catch (Exception e) {
        // E.g. deleted: its parent has a record too (found missing there).
        Logger.LogInfo("Change journal self-check: cannot list \"{0}\": {1}", rootPath.Combine(relativePath), e.Message);
        return;
      }

      RelativePath Child(string name) => relativePath.IsEmpty ? new RelativePath(name) : relativePath.CreateChild(name);

      var files = new Dictionary<string, (FileMetadata Metadata, bool IsSymLink)>(SystemPathComparer.NameComparer);
      var directories = new Dictionary<string, DirectoryEntry>(SystemPathComparer.NameComparer);
      foreach (var entry in entries) {
        var path = Child(entry.Name);
        if (entry.IsDirectory) {
          if (project.DirectoryFilter.Include(path.Value.AsSpan()))
            directories[entry.Name] = entry;
        } else if (entry.IsFile) {
          if (project.FileFilter.Include(path.Value.AsSpan()))
            files[entry.Name] = (entry.Metadata, entry.IsSymLink);
        }
      }

      // Files
      for (var i = 0; i < directory.ChildFiles.Count; i++) {
        var file = directory.ChildFiles[i];
        if (!files.Remove(file.Name, out var listed)) {
          changes.Add(new PathChangeEntry(rootPath, file.RelativePath, PathChangeKind.Deleted, PathKind.File));
          continue;
        }
        // Note: The metadata of the symbolic links are checked again by the catch-up.
        if (listed.IsSymLink || directory.IsSymLinkFile(i))
          continue;
        if (listed.Metadata != savedMetadata(file, directory.ChildFileMetadata[i])) {
          changes.Add(new PathChangeEntry(rootPath, file.RelativePath, PathChangeKind.Changed, PathKind.File));
        }
      }
      foreach (var name in files.Keys.OrderBy(x => x, SystemPathComparer.NameComparer)) {
        changes.Add(new PathChangeEntry(rootPath, Child(name), PathChangeKind.Created, PathKind.File));
      }

      // Directories
      foreach (var child in directory.ChildDirectories) {
        var name = child.DirectoryName.Name;
        if (!directories.Remove(name, out var listed)) {
          changes.Add(new PathChangeEntry(rootPath, child.DirectoryName.RelativePath, PathChangeKind.Deleted, PathKind.Directory));
          continue;
        }
        // Note: The id of a directory symbolic link is the id of its target (on the volume of the
        // project, see DirectoryData.FileId).
        var listedId = listed.IsSymLink
          ? ProjectRootSnapshotBuilder.GetSymLinkTargetId(_fileSystem, rootPath.Combine(Child(listed.Name)), projectVolume)
          : listed.FileId;
        var replaced = listed.IsSymLink != child.IsSymLink ||
                       !string.Equals(listed.Name, name, StringComparison.Ordinal) ||
                       ((child.FileId != UInt128.Zero || child.IsSymLink) && listedId != child.FileId);
        if (replaced) {
          changes.Add(new PathChangeEntry(rootPath, child.DirectoryName.RelativePath, PathChangeKind.Deleted, PathKind.Directory));
          changes.Add(new PathChangeEntry(rootPath, Child(listed.Name), PathChangeKind.Created, PathKind.Directory));
        }
      }
      foreach (var name in directories.Keys.OrderBy(x => x, SystemPathComparer.NameComparer)) {
        changes.Add(new PathChangeEntry(rootPath, Child(name), PathChangeKind.Created, PathKind.Directory));
      }
    }
  }

  /// <summary>The result of a <see cref="ChangeJournalSelfCheck"/>.</summary>
  public sealed class ChangeJournalSelfCheckResult {
    /// <summary>The checkpoint to save.</summary>
    public ChangeJournalCheckpoint Checkpoint { get; set; }
    /// <summary>Why the journal was not read (the current checkpoint is kept), or null.</summary>
    public string FailureReason { get; set; }
    public long RecordCount { get; set; }
    public int ListedDirectoryCount { get; set; }
    /// <summary>The differences between the tree and the file system (missed changes).</summary>
    public List<PathChangeEntry> Changes { get; } = new List<PathChangeEntry>();
  }
}
