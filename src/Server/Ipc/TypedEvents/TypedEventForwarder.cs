// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Operations;
using VsChromium.Server.Search;

namespace VsChromium.Server.Ipc.TypedEvents {
  [Export(typeof(ITypedEventForwarder))]
  public class TypedEventForwarder : ITypedEventForwarder {
    private readonly IFileSystemSnapshotManager _fileSystemSnapshotManager;
    private readonly ISearchEngine _searchEngine;
    private readonly ITypedEventSender _typedEventSender;
    private readonly IIndexingServer _indexingServer;

    [ImportingConstructor]
    public TypedEventForwarder(
      ITypedEventSender typedEventSender,
      IIndexingServer indexingServer,
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      ISearchEngine searchEngine) {
      _typedEventSender = typedEventSender;
      _indexingServer = indexingServer;
      _fileSystemSnapshotManager = fileSystemSnapshotManager;
      _searchEngine = searchEngine;
    }

    public void RegisterEventHandlers() {
      _fileSystemSnapshotManager.SnapshotScanStarted += FileSystemSnapshotManagerOnSnapshotScanStarted;
      _fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;

      _searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      _searchEngine.FilesLoadingProgress += SearchEngineOnFilesLoadingProgress;
      _searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;

      _indexingServer.StateUpdated += IndexingServerOnStateUpdated;
    }

    private void IndexingServerOnStateUpdated(object sender, IndexingServerStateUpdatedEventArgs e) {
      _typedEventSender.SendEventAsync(new IndexingServerStateChangedEvent {
        ServerStatus = e.State.Status,
        LastIndexUpdatedUtc = e.State.LastIndexUpdateUtc,
      });
    }

    private void FileSystemSnapshotManagerOnSnapshotScanStarted(object sender, OperationInfo e) {
      _typedEventSender.SendEventAsync(new FileSystemScanStarted {
        OperationId = e.OperationId
      });
    }

    private void FileSystemSnapshotManagerOnSnapshotScanFinished(object sender, SnapshotScanResult e) {
      var fileSystemTreeComputed = new FileSystemScanFinished {
        OperationId = e.OperationInfo.OperationId,
        Error = ErrorResponseHelper.CreateErrorResponse(e.Error),
      };

      if (e.PreviousSnapshot != null) {
        fileSystemTreeComputed.OldVersion = e.PreviousSnapshot.Version;
        fileSystemTreeComputed.NewVersion = e.NewSnapshot.Version;
      }

      _typedEventSender.SendEventAsync(fileSystemTreeComputed);
    }

    private void SearchEngineOnFilesLoading(object sender, OperationInfo args) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoading {
        OperationId = args.OperationId
      });
    }

    private void SearchEngineOnFilesLoadingProgress(object sender, OperationInfo args) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoadingProgress {
        OperationId = args.OperationId
      });
    }

    private void SearchEngineOnFilesLoaded(object sender, FilesLoadedResult args) {
      _typedEventSender.SendEventAsync(new SearchEngineFilesLoaded {
        OperationId = args.OperationInfo.OperationId,
        Error = ErrorResponseHelper.CreateErrorResponse(args.Error),
        TreeVersion = args.TreeVersion,
      });
    }
  }
}
