// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Linq;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystem;
using VsChromiumServer.FileSystemNames;
using VsChromiumServer.Search;

namespace VsChromiumServer.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class SearchDirectoryNamesRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public SearchDirectoryNamesRequestHandler(ISearchEngine searchEngine, IFileSystemNameFactory fileSystemNameFactory) {
      _searchEngine = searchEngine;
      _fileSystemNameFactory = fileSystemNameFactory;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var r = (SearchDirectoryNamesRequest)typedRequest;
      var result = _searchEngine.SearchDirectoryNames(r.SearchParams);
      return new SearchDirectoryNamesResponse {
        DirectoryNames = _fileSystemNameFactory.ToFlatSearchResult(result)
      };
    }
  }

  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetFileExtractsRequestHandler : TypedMessageRequestHandler {
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetFileExtractsRequestHandler(ISearchEngine searchEngine) {
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetFileExtractsRequest)typedRequest;
      var result = _searchEngine.GetFileExtracts(request.FileName, request.Positions);
      return new GetFileExtractsResponse {
        FileName = request.FileName,
        FileExtracts = result.ToList()
      };
    }
  }
}
