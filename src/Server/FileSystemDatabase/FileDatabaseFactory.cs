// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemScanSnapshot;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  [Export(typeof(IFileDatabaseFactory))]
  public class FileDatabaseFactory : IFileDatabaseFactory {
    private readonly IFileSystem _fileSystem;

    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileDatabaseFactory(IFileSystem fileSystem, IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase CreateEmpty() {
      return new FileDatabase(
          new Dictionary<FullPath, string>(), 
          new Dictionary<FileName, FileData>(),
          new List<FileName>(), 
          new Dictionary<DirectoryName, DirectoryData>(),
          new List<IFileContentsPiece>(),
          0);
    }

    public IFileDatabase CreateIncremental(
      IFileDatabase previousFileDatabase,
      FileSystemTreeSnapshot previousSnapshot,
      FileSystemTreeSnapshot newSnapshot,
      FullPathChanges fullPathChanges,
      Action<IFileDatabase> onIntermadiateResult) {

      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory)
          .Build(previousFileDatabase, newSnapshot, fullPathChanges, onIntermadiateResult);
    }

    /// <summary>
    /// Atomically updates the file contents of <paramref name="changedFiles"/>
    /// with the new file contents on disk. This method violates the "pure
    /// snapshot" semantics but enables efficient updates for the most common
    /// type of file change events.
    /// </summary>
    public IFileDatabase CreateWithChangedFiles(
      IFileDatabase previousFileDatabase,
      IEnumerable<ProjectFileName> changedFiles,
      Action onLoading,
      Action onLoaded) {
      return new FileDatabaseBuilder(_fileSystem, _fileContentsFactory, _progressTrackerFactory)
        .BuildWithChangedFiles(previousFileDatabase, changedFiles, onLoading, onLoaded);
    }
  }
}
