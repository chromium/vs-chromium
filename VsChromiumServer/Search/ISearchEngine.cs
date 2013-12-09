// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystemNames;

namespace VsChromiumServer.Search {
  public interface ISearchEngine {
    IEnumerable<FileName> SearchFileNames(SearchParams searchParams);
    IEnumerable<DirectoryName> SearchDirectoryNames(SearchParams searchParams);
    IEnumerable<FileSearchResult> SearchFileContents(SearchParams searchParams);
    IEnumerable<FileExtract> GetFileExtracts(string filename, IEnumerable<FilePositionSpan> spans);

    event Action<long> FilesLoading;
    event Action<long> FilesLoaded;
  }
}
