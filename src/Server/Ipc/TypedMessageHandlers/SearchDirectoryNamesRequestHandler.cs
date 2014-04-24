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
        SearchResult = FileSystemNameFactoryExtensions.ToFlatSearchResult(_fileSystemNameFactory, result.DirectoryNames),
        HitCount = result.DirectoryNames.Count,
        TotalCount = result.TotalCount
      };
    }
  }
}
