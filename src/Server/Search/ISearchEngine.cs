// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.Operations;

namespace VsChromium.Server.Search {
  public interface ISearchEngine {
    SearchFileNamesResult SearchFileNames(SearchParams searchParams);
    SearchDirectoryNamesResult SearchDirectoryNames(SearchParams searchParams);
    SearchFileContentsResult SearchFileContents(SearchParams searchParams);
    IEnumerable<FileExtract> GetFileExtracts(FullPath filename, IEnumerable<FilePositionSpan> spans);

    event EventHandler<OperationInfo> FilesLoading;
    event EventHandler<FilesLoadedResult> FilesLoaded;
  }
}
