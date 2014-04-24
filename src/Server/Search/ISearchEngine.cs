// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  public interface ISearchEngine {
    SearchFileNamesResult SearchFileNames(SearchParams searchParams);
    SearchDirectoryNamesResult SearchDirectoryNames(SearchParams searchParams);
    SearchFileContentsResult SearchFileContents(SearchParams searchParams);
    IEnumerable<FileExtract> GetFileExtracts(FullPathName filename, IEnumerable<FilePositionSpan> spans);

    event Action<long> FilesLoading;
    event Action<long> FilesLoaded;
  }
}
