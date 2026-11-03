// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Protocol.Generated;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Operations;
using VsChromium.Server.Search;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// Forwards the events of the services of the server to the client, as notifications of the
  /// protocol ("index/*" notifications).
  /// </summary>
  public sealed class JsonRpcEventForwarder : IDisposable {
    private readonly ProtocolNotificationSender _notificationSender;
    private readonly IIndexingServer _indexingServer;
    private readonly IFileSystemSnapshotManager _fileSystemSnapshotManager;
    private readonly ISearchEngine _searchEngine;
    private bool _registered;

    public JsonRpcEventForwarder(
      ProtocolNotificationSender notificationSender,
      IIndexingServer indexingServer,
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      ISearchEngine searchEngine) {
      _notificationSender = notificationSender;
      _indexingServer = indexingServer;
      _fileSystemSnapshotManager = fileSystemSnapshotManager;
      _searchEngine = searchEngine;
    }

    public void RegisterEventHandlers() {
      if (_registered) {
        return;
      }
      _registered = true;
      _fileSystemSnapshotManager.SnapshotScanStarted += OnSnapshotScanStarted;
      _fileSystemSnapshotManager.SnapshotScanFinished += OnSnapshotScanFinished;
      _searchEngine.FilesLoading += OnFilesLoading;
      _searchEngine.FilesLoadingProgress += OnFilesLoadingProgress;
      _searchEngine.FilesLoaded += OnFilesLoaded;
      _indexingServer.StateUpdated += OnIndexingServerStateUpdated;
    }

    public void Dispose() {
      if (!_registered) {
        return;
      }
      _registered = false;
      _fileSystemSnapshotManager.SnapshotScanStarted -= OnSnapshotScanStarted;
      _fileSystemSnapshotManager.SnapshotScanFinished -= OnSnapshotScanFinished;
      _searchEngine.FilesLoading -= OnFilesLoading;
      _searchEngine.FilesLoadingProgress -= OnFilesLoadingProgress;
      _searchEngine.FilesLoaded -= OnFilesLoaded;
      _indexingServer.StateUpdated -= OnIndexingServerStateUpdated;
    }

    /// <summary>The protocol value of an indexing status.</summary>
    public static IndexingStatus ToIndexingStatus(IndexingServerStatus status) {
      switch (status) {
        case IndexingServerStatus.Idle:
          return IndexingStatus.Idle;
        case IndexingServerStatus.Paused:
          return IndexingStatus.Paused;
        case IndexingServerStatus.Yield:
          return IndexingStatus.Yield;
        case IndexingServerStatus.Busy:
          return IndexingStatus.Busy;
        default:
          throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown indexing status");
      }
    }

    private void OnIndexingServerStateUpdated(object sender, IndexingServerStateUpdatedEventArgs e) {
      _notificationSender.IndexStateChanged(new IndexStateChangedParams {
        Status = ToIndexingStatus(e.State.Status),
        LastIndexUpdatedUtc = e.State.LastIndexUpdateUtc,
      });
    }

    private void OnSnapshotScanStarted(object sender, OperationInfo e) {
      _notificationSender.IndexScanStarted(new OperationStartedParams {
        OperationId = e.OperationId,
      });
    }

    private void OnSnapshotScanFinished(object sender, SnapshotScanResult e) {
      var parameters = new ScanFinishedParams {
        OperationId = e.OperationInfo.OperationId,
        Error = JsonRpcErrors.CreateErrorInfo(e.Error),
      };
      if (e.PreviousSnapshot != null) {
        parameters.OldVersion = e.PreviousSnapshot.Version;
        parameters.NewVersion = e.NewSnapshot.Version;
      }
      _notificationSender.IndexScanFinished(parameters);
    }

    private void OnFilesLoading(object sender, OperationInfo e) {
      _notificationSender.IndexLoading(new OperationStartedParams {
        OperationId = e.OperationId,
      });
    }

    private void OnFilesLoadingProgress(object sender, OperationInfo e) {
      _notificationSender.IndexLoadingProgress(new OperationStartedParams {
        OperationId = e.OperationId,
      });
    }

    private void OnFilesLoaded(object sender, FilesLoadedResult e) {
      _notificationSender.IndexLoaded(new FilesLoadedParams {
        OperationId = e.OperationInfo.OperationId,
        TreeVersion = e.TreeVersion,
        Error = JsonRpcErrors.CreateErrorInfo(e.Error),
      });
    }
  }
}
