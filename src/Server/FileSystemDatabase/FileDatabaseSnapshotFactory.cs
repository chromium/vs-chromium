// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes a in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemSnapshot"/> snapshot.
  /// </summary>
  [Export(typeof(IFileDatabaseSnapshotFactory))]
  public class FileDatabaseSnapshotFactory : IFileDatabaseSnapshotFactory {
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private readonly IFileContentsFactory _fileContentsFactory;

    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileDatabaseSnapshotFactory(IFileSystem fileSystem, IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory) {

      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabaseSnapshot CreateEmpty() {
      return new FileDatabaseSnapshot(
        new Dictionary<FullPath, string>(),
        new Dictionary<FileName, FileWithContents>(),
        new List<FileName>(),
        new Dictionary<DirectoryName, DirectoryData>(),
        new List<IFileContentsPiece>(),
        0);
    }

    public IFileDatabaseSnapshot CreateIncremental(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot, FullPathChanges fullPathChanges,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {

      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory)
        .Build(previousDatabase, newFileSystemSnapshot, fullPathChanges,
          onLoading, onLoaded, onIntermadiateResult, cancellationToken);
    }

    /// <summary>
    /// Atomically updates the file contents of <paramref name="changedFiles"/>
    /// with the new file contents on disk. This method violates the "pure
    /// snapshot" semantics but enables efficient updates for the most common
    /// type of file change events.
    /// </summary>
    public IFileDatabaseSnapshot CreateWithChangedFiles(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot fileSystemSnapshot, IList<ProjectFileName> changedFiles,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {

      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory)
        .BuildWithChangedFiles(previousDatabase, fileSystemSnapshot, changedFiles,
          onLoading, onLoaded, onIntermadiateResult, cancellationToken);
    }
  }
}