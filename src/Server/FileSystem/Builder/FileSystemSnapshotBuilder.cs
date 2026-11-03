// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
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

    public FileSystemSnapshotBuilder(
      IFileSystem fileSystem,
      IProgressTrackerFactory progressTrackerFactory,
      IIndexingWorkerPool indexingWorkerPool) {
      _fileSystem = fileSystem;
      _progressTrackerFactory = progressTrackerFactory;
      _indexingWorkerPool = indexingWorkerPool;
    }

    public FileSystemSnapshot Compute(IFileSystemNameFactory fileNameFactory,
                                          FileSystemSnapshot oldSnapshot,
                                          FullPathChanges pathChanges/* may be null */,
                                          IList<IProject> projects,
                                          int version,
                                          CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested(); // cancellation
      using (OperationTimings.Measure(OperationTimings.FileSystemScan))
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        try {
          var projectRoots =
            projects
              .Distinct(new ProjectPathComparer())
              .Select(project => {
                cancellationToken.ThrowIfCancellationRequested(); // cancellation

                var projectSnapshotBuilder = new ProjectRootSnapshotBuilder(
                  _fileSystem, fileNameFactory, oldSnapshot, project, progress,
                  (pathChanges == null ? null : new ProjectPathChanges(project.RootPath, pathChanges.Entries)),
                  _indexingWorkerPool,
                  cancellationToken);

                var rootSnapshot = projectSnapshotBuilder.Build();

                return new ProjectRootSnapshot(project, rootSnapshot);
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
  }
}
