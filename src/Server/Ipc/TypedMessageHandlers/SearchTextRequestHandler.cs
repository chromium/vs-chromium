// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class SearchTextRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public SearchTextRequestHandler(ISearchEngine searchEngine, IFileSystemNameFactory fileSystemNameFactory) {
      _searchEngine = searchEngine;
      _fileSystemNameFactory = fileSystemNameFactory;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (SearchTextRequest)typedRequest;
      var result = _searchEngine.SearchFileContents(request.SearchParams);
      var searchResults = FileSystemNameFactoryExtensions.ToFlatSearchResult(
        _fileSystemNameFactory,
        result.Entries,
        searchResult => searchResult.FileName,
        searchResult => new FilePositionsData { Positions = searchResult.Spans });
      return new SearchTextResponse {
        SearchResults = searchResults,
        HitCount = result.HitCount,
        TotalFileCount = result.TotalFileCount,
        SearchedFileCount = result.SearchedFileCount
      };
    }
  }
}
