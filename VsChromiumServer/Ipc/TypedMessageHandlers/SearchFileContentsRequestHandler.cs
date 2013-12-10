// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;
using VsChromiumServer.FileSystemNames;
using VsChromiumServer.Search;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class SearchFileContentsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public SearchFileContentsRequestHandler(ISearchEngine searchEngine, IFileSystemNameFactory fileSystemNameFactory) {
      _searchEngine = searchEngine;
      _fileSystemNameFactory = fileSystemNameFactory;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (SearchFileContentsRequest)typedRequest;
      var result = _searchEngine.SearchFileContents(request.SearchParams);
      return new SearchFileContentsResponse {
        SearchResults = _fileSystemNameFactory.ToFlatSearchResult(
          result,
          searchResult => searchResult.FileName,
          searchResult => new FilePositionsData {
            Positions = searchResult.Spans
          })
      };
    }
  }
}
