// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.Cache;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes a in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemSnapshot"/> snapshot.
  /// </summary>
  public class FileDatabaseSnapshotFactory : IFileDatabaseSnapshotFactory {
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private readonly IFileContentsFactory _fileContentsFactory;

    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly IIndexingWorkerPool _indexingWorkerPool;
    private readonly IIndexCache _indexCache;

    public FileDatabaseSnapshotFactory(
      IFileSystem fileSystem,
      IFileContentsFactory fileContentsFactory,
      IProgressTrackerFactory progressTrackerFactory,
      IIndexingWorkerPool indexingWorkerPool,
      IIndexCache indexCache = null) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
      _indexingWorkerPool = indexingWorkerPool;
      _indexCache = indexCache;
    }

    public IFileDatabaseSnapshot CreateEmpty() {
      return FileDatabaseSnapshot.Empty();
    }

    public IFileDatabaseSnapshot CreateIncremental(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot, Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult,
      CancellationToken cancellationToken) {

      return CreateIncrementalWorker(previousDatabase, newFileSystemSnapshot, null, onLoading, onLoaded,
        onIntermadiateResult,
        cancellationToken);
    }

    public IFileDatabaseSnapshot CreateIncrementalWithFileSystemUpdates(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot, FullPathChanges fullPathChanges, Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {

      return CreateIncrementalWorker(previousDatabase, newFileSystemSnapshot, fullPathChanges, onLoading, onLoaded,
        onIntermadiateResult,
        cancellationToken);
    }

    /// <summary>
    /// Atomically updates the file contents of <paramref name="changedFiles"/>
    /// with the new file contents on disk. This method violates the "pure
    /// snapshot" semantics but enables efficient updates for the most common
    /// type of file change events.
    /// </summary>
    public IFileDatabaseSnapshot CreateIncrementalWithModifiedFiles(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot fileSystemSnapshot, IList<ProjectFileName> changedFiles,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {

      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory, _indexingWorkerPool)
        .BuildWithChangedFiles(previousDatabase, fileSystemSnapshot, changedFiles,
          onLoading, onLoaded, onIntermadiateResult, cancellationToken);
    }

    private IFileDatabaseSnapshot CreateIncrementalWorker(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot, FullPathChanges fullPathChanges,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken) {

      // Note: Full loads only use the index cache (the contents of changed files are read from disk).
      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory, _indexingWorkerPool, _indexCache)
        .Build(previousDatabase, newFileSystemSnapshot, fullPathChanges,
          onLoading, onLoaded, onIntermadiateResult, cancellationToken);
    }
  }
}
