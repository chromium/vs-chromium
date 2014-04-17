// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.ProgressTracking;

namespace VsChromium.Server.Search {
  /// <summary>
  /// Exposes am in-memory snapshot of the list of file names, directory names
  /// and file contents for a given <see cref="FileSystemTreeSnapshot"/> snapshot.
  /// </summary>
  [Export(typeof(IFileDatabaseFactory))]
  public class FileDatabaseFactory : IFileDatabaseFactory {
    /// <summary>
    /// Note: For debugging purposes only.
    /// </summary>
    private readonly IFileContentsFactory _fileContentsFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileDatabaseFactory(IFileContentsFactory fileContentsFactory, IProgressTrackerFactory progressTrackerFactory) {
      _fileContentsFactory = fileContentsFactory;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public IFileDatabase CreateEmpty() {
      return new FileDatabase(_fileContentsFactory, new Dictionary<FileName, FileData>(), new List<FileName>(), new List<DirectoryName>(), new List<FileData>());
    }

    public IFileDatabase CreateIncremental(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot) {
      return new FileDatabaseBuilder(_fileContentsFactory, _progressTrackerFactory).Build(previousFileDatabase, newSnapshot);
    }
  }
}
