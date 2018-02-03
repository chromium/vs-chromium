// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
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

      if (request.ForceGabageCollection) {
        using (new TimeElapsedLogger("Forcing a full gabarge collection before returning statistics")) {
          GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
        }
      }

      var snapshot = _snapshotManager.CurrentSnapshot;
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      var indexingServerState = _indexingServer.CurrentState;
      return new GetDatabaseStatisticsResponse {
        ProjectCount = snapshot.ProjectRoots.Count,
        FileCount = database.FileNames.Count,
        SearchableFileCount = database.SearchableFileCount,
        ServerNativeMemoryUsage = database.TotalFileContentsLength,
        IndexLastUpdatedUtc = indexingServerState.LastIndexUpdateUtc,
        ServerGcMemoryUsage = GC.GetTotalMemory(false),
        ServerStatus = indexingServerState.Status,
      };
    }
  }
}