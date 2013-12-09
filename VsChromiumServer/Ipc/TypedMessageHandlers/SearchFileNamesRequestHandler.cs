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
  public class SearchFileNamesRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public SearchFileNamesRequestHandler(ISearchEngine searchEngine, IFileSystemNameFactory fileSystemNameFactory) {
      this._searchEngine = searchEngine;
      this._fileSystemNameFactory = fileSystemNameFactory;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (SearchFileNamesRequest)typedRequest;
      var result = this._searchEngine.SearchFileNames(request.SearchParams);
      return new SearchFileNamesResponse {
        FileNames = this._fileSystemNameFactory.ToFlatSearchResult(result)
      };
    }
  }
}
