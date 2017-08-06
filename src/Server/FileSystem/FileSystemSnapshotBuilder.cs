// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemScanSnapshot {
  [Export(typeof(IFileSystemSnapshotBuilder))]
  public class FileSystemSnapshotBuilder : IFileSystemSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileSystemSnapshotBuilder(
      IFileSystem fileSystem,
      IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public FileSystemSnapshot Compute(IFileSystemNameFactory fileNameFactory,
                                          FileSystemSnapshot oldSnapshot,
                                          FullPathChanges pathChanges/* may be null */,
                                          IList<IProject> projects,
                                          int version,
                                          CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested(); // cancellation
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var projectRoots =
          projects
            .Distinct(new ProjectPathComparer())
            .Select(project => {
              cancellationToken.ThrowIfCancellationRequested(); // cancellation

              var projectSnapshotBuilder = new ProjectRootSnapshotBuilder(
                _fileSystem, fileNameFactory, oldSnapshot, project, progress,
                (pathChanges == null ? null : new ProjectPathChanges(project.RootPath, pathChanges.Entries)),
                cancellationToken);

              var rootSnapshot = projectSnapshotBuilder.Build();

              return new ProjectRootSnapshot(project, rootSnapshot);
            })
            .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
            .ToReadOnlyCollection();

        return new FileSystemSnapshot(version, projectRoots);
      }
    }
  }
}
