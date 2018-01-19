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
    private readonly IFileSystemSnapshotManager _snapshotManager;
    private readonly ISearchEngine _searchEngine;
    private readonly IIndexingServer _indexingServer;

    [ImportingConstructor]
    public GetDatabaseStatisticsRequestHandler(IFileSystemSnapshotManager snapshotManager, ISearchEngine searchEngine, IIndexingServer indexingServer) {
      _snapshotManager = snapshotManager;
      _searchEngine = searchEngine;
      _indexingServer = indexingServer;
    }

    public override TypedResponse Process(TypedRequest typedRequest) {
      var request = (GetDatabaseStatisticsRequest)typedRequest;

      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var indexingServerState = _indexingServer.CurrentState;
      return new GetDatabaseStatisticsResponse {
        ProjectCount = snapshot.ProjectRoots.Count,
        FileCount = database.FileNames.Count,
        IndexedFileCount = database.SearchableFileCount,
        IndexedFileSize = database.FileContentsPieces.Aggregate(0L, (x, piece) => x + piece.ByteLength),
        IndexingPaused = indexingServerState.Status == IndexingServerStatus.Paused,
        IndexingPausedReason = indexingServerState.PauseReason == IndexingServerPauseReason.UserRequest ? IndexingPausedReason.UserAction : IndexingPausedReason.FileSystemWatcherOverflow,
        IndexLastUpdatedUtc = indexingServerState.LastIndexUpdateUtc,
      };
    }
  }
}