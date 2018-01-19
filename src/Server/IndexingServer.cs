// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Threads;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Search;
using VsChromium.Server.Threads;

namespace VsChromium.Server {
  [Export(typeof(IIndexingServer))]
  public class IndexingServer : IIndexingServer {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileSystemSnapshotManager _fileSystemSnapshotManager;
    private readonly ITaskQueue _stateChangeTaskQueue;

    private IndexingServerStatus _status;
    private IndexingServerPauseReason _pauseReason;
    private DateTime _lastUpdateUtc;

    [ImportingConstructor]
    public IndexingServer(
      IDateTimeProvider dateTimeProvider,
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      ITaskQueueFactory taskQueueFactory,
      ISearchEngine searchEngine) {
      _dateTimeProvider = dateTimeProvider;
      _fileSystemSnapshotManager = fileSystemSnapshotManager;
      _stateChangeTaskQueue = taskQueueFactory.CreateQueue("IndexingServer State Change Task Queue");

      _fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;
      _fileSystemSnapshotManager.FileSystemWatchStopped += FileSystemSnapshotManagerOnFileSystemWatchStopped;
      searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
    }

    public event EventHandler<IndexingServerStateUpdatedEventArgs> StateUpdated;

    public void TogglePausedRunning() {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        switch (_status) {
          case IndexingServerStatus.Running:
            PauseImpl(IndexingServerPauseReason.UserRequest);
            break;
          case IndexingServerStatus.Paused:
            Resume();
            break;
          default:
            throw new ArgumentOutOfRangeException();
        }
      });
    }

    public IndexingServerState CurrentState {
      get {
        return new IndexingServerState {
          Status = _status,
          PauseReason = _pauseReason,
          LastIndexUpdateUtc = _lastUpdateUtc,
        };
      }
    }

    public void Pause() {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        PauseImpl(IndexingServerPauseReason.UserRequest);
      });
    }

    public void Resume() {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        ResumeImpl();
      });
    }

    private void PauseImpl(IndexingServerPauseReason reason) {
      switch (_status) {
        case IndexingServerStatus.Running:
          _status = IndexingServerStatus.Paused;
          _pauseReason = reason;
          _fileSystemSnapshotManager.Pause();
          OnStatusUpdated();
          break;
        case IndexingServerStatus.Paused:
          return;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    private void ResumeImpl() {
      switch (_status) {
        case IndexingServerStatus.Running:
          return;
        case IndexingServerStatus.Paused:
          _status = IndexingServerStatus.Running;
          _pauseReason = default(IndexingServerPauseReason);
          _fileSystemSnapshotManager.Resume();
          OnStatusUpdated();
          return;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    private void FileSystemSnapshotManagerOnSnapshotScanFinished(object sender, SnapshotScanResult snapshotScanResult) {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        if (snapshotScanResult.Error == null) {
          _lastUpdateUtc = _dateTimeProvider.UtcNow;
          OnStatusUpdated();
        }
      });
    }

    private void SearchEngineOnFilesLoaded(object sender, FilesLoadedResult filesLoadedResult) {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        if (filesLoadedResult.Error == null) {
          _lastUpdateUtc = _dateTimeProvider.UtcNow;
          OnStatusUpdated();
        }
      });
    }

    private void FileSystemSnapshotManagerOnFileSystemWatchStopped(object sender, FileSystemWatchStoppedEventArgs e) {
      _stateChangeTaskQueue.EnqueueUnique(token => {
        if (_status == IndexingServerStatus.Running) {
          _status = IndexingServerStatus.Paused;
          _pauseReason = e.IsError
            ? IndexingServerPauseReason.FileWatchBufferOverflow
            : IndexingServerPauseReason.UserRequest;
          OnStatusUpdated();
        }
      });
    }

    protected virtual void OnStatusUpdated() {
      var e = new IndexingServerStateUpdatedEventArgs {State = CurrentState};
      StateUpdated?.Invoke(this, e);
    }
  }
}