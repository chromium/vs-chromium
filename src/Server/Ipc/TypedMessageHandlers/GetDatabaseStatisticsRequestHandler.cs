// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedMessageHandlers {
  [Export(typeof(ITypedMessageRequestHandler))]
  public class GetDatabaseStatisticsRequestHandler : TypedMessageRequestHandler {
    private readonly IFileSystemProcessor _processor;
    private readonly ISearchEngine _searchEngine;

    [ImportingConstructor]
    public GetDatabaseStatisticsRequestHandler(IFileSystemProcessor processor, ISearchEngine searchEngine) {
      _processor = processor;
      _searchEngine = searchEngine;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDatabaseStatisticsRequest)typedRequest;

      var snapshot = _processor.GetCurrentSnapshot();
      var database = _searchEngine.CurrentFileDatabase;
      return new GetDatabaseStatisticsResponse {
        ProjectCount = snapshot.ProjectRoots.Count,
        DirectoryCount = database.DirectoryNames.Count,
        FileCount = database.FileNames.Count,
        IndexedFileCount = database.SearchableFileCount,
        IndexedFileSize = database.FileContentsPieces.GroupBy(x => x.FileId).Aggregate(0L, (x, group) => x + group.First().FileContents.ByteLength),
      };
    }
  }
}