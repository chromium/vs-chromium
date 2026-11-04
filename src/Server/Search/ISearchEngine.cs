// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.Operations;

namespace VsChromium.Server.Search {
  public interface ISearchEngine {
    IFileDatabaseSnapshot CurrentFileDatabaseSnapshot { get; }

    /// <summary>
    /// The current database if it is the result of a complete update of the file system snapshot
    /// <paramref name="fileSystemSnapshotVersion"/>, otherwise null (e.g. a load is in progress).
    /// </summary>
    IFileDatabaseSnapshot GetCompleteDatabase(int fileSystemSnapshotVersion);

    SearchFilePathsResult SearchFilePaths(SearchParams searchParams, CancellationToken cancellationToken = default);
    SearchCodeResult SearchCode(SearchParams searchParams, CancellationToken cancellationToken = default);
    IEnumerable<FileExtract> GetFileExtracts(FullPath filename, IEnumerable<FilePositionSpan> spans, int maxLength);

    /// <summary>
    /// Changes the options of the project containing <paramref name="path"/> (see
    /// "project/setOptions"). Null values leave the option unchanged.
    /// </summary>
    void SetProjectOptions(FullPath path, bool? trigramFiltersEnabled);

    /// <summary>
    /// The root path of the project containing <paramref name="path"/> (also for projects not loaded
    /// yet), or <paramref name="path"/> if none.
    /// </summary>
    FullPath GetProjectRoot(FullPath path);

    /// <summary>True if the trigram filters of the project are enabled (the default).</summary>
    bool IsTrigramFiltersEnabled(FullPath projectRootPath);

    /// <summary>Memory used by the trigram filters of the project.</summary>
    long GetTrigramFiltersBytes(FullPath projectRootPath);

    event EventHandler<OperationInfo> FilesLoading;
    event EventHandler<OperationInfo> FilesLoadingProgress;
    event EventHandler<FilesLoadedResult> FilesLoaded;
  }
}
