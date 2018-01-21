// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Threads;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Operations;
using VsChromium.Server.Search;
using VsChromium.Server.Threads;

namespace VsChromium.Server {
  [Export(typeof(IIndexingServer))]
  public class IndexingServer : IIndexingServer {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileSystemSnapshotManager _fileSystemSnapshotManager;
    private readonly ITaskQueue _stateChangeTaskQueue;

    private bool _paused;
    private bool _indexing;
    private bool _pausedDueToError;
    private DateTime _lastUpdateUtc = DateTime.MinValue;

    [ImportingConstructor]
    public IndexingServer(
      IDateTimeProvider dateTimeProvider,
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      ITaskQueueFactory taskQueueFactory,
      ISearchEngine searchEngine) {
      _dateTimeProvider = dateTimeProvider;
      _fileSystemSnapshotManager = fileSystemSnapshotManager;
      _stateChangeTaskQueue = taskQueueFactory.CreateQueue("IndexingServer State Change Task Queue");

      _fileSystemSnapshotManager.SnapshotScanStarted += FileSystemSnapshotManagerOnSnapshotScanStarted;
      _fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;
      _fileSystemSnapshotManager.FileSystemWatchStopped += FileSystemSnapshotManagerOnFileSystemWatchStopped;
      _fileSystemSnapshotManager.FileSystemWatchResumed += FileSystemSnapshotManagerOnFileSystemWatchResumed;

      searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
    }

    public IndexingServerState CurrentState {
      get {
        IndexingServerState result = null;
        var e = new ManualResetEvent(false);
        _stateChangeTaskQueue.ExecuteAsync(token => {
          result = GetCurrentState();
          e.Set();
        });
        e.WaitOne();
        Debug.Assert(result != null);
        return result;
      }
    }

    private IndexingServerStatus GetStatus() {
      if (_indexing)
        return IndexingServerStatus.Busy;
      if (_pausedDueToError)
        return IndexingServerStatus.Yield;
      if (_paused)
        return IndexingServerStatus.Paused;
      return IndexingServerStatus.Idle;
    }

    public event EventHandler<IndexingServerStateUpdatedEventArgs> StateUpdated;

    public void Refresh() {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _fileSystemSnapshotManager.Refresh();
      });
    }

    public void TogglePausedRunning() {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        if (_paused) {
          ResumeImpl();
        } else {
          PauseImpl(IndexingServerPauseReason.UserRequest);
        }
      });
    }

    public void Pause() {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        PauseImpl(IndexingServerPauseReason.UserRequest);
      });
    }

    public void Resume() {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        ResumeImpl();
      });
    }

    private void PauseImpl(IndexingServerPauseReason reason) {
      _paused = true;
      _pausedDueToError = (reason == IndexingServerPauseReason.FileWatchBufferOverflow);
      _fileSystemSnapshotManager.Pause();
      OnStatusUpdated();
    }

    private void ResumeImpl() {
      _paused = false;
      _pausedDueToError = false;
      _fileSystemSnapshotManager.Resume();
      OnStatusUpdated();
    }

    private void FileSystemSnapshotManagerOnSnapshotScanStarted(object sender, OperationInfo operationInfo) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _indexing = true;
        OnStatusUpdated();
      });
    }

    private void FileSystemSnapshotManagerOnSnapshotScanFinished(object sender, SnapshotScanResult snapshotScanResult) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _indexing = false;
        if (snapshotScanResult.Error == null) {
          _lastUpdateUtc = _dateTimeProvider.UtcNow;
        }
        OnStatusUpdated();
      });
    }

    private void SearchEngineOnFilesLoading(object sender, OperationInfo operationInfo) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _indexing = true;
        OnStatusUpdated();
      });
    }

    private void SearchEngineOnFilesLoaded(object sender, FilesLoadedResult filesLoadedResult) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _indexing = false;
        if (filesLoadedResult.Error == null) {
          _lastUpdateUtc = _dateTimeProvider.UtcNow;
        }
        OnStatusUpdated();
      });
    }

    private void FileSystemSnapshotManagerOnFileSystemWatchStopped(object sender, FileSystemWatchStoppedEventArgs e) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        if (!_paused) {
          _paused = true;
          _pausedDueToError = e.IsError;
          OnStatusUpdated();
        }
      });
    }

    private void FileSystemSnapshotManagerOnFileSystemWatchResumed(object sender, EventArgs e) {
      _stateChangeTaskQueue.ExecuteAsync(token => {
        if (_paused) {
          _paused = false;
          _pausedDueToError = false;
          OnStatusUpdated();
        }
      });
    }

    protected virtual void OnStatusUpdated() {
      var e = new IndexingServerStateUpdatedEventArgs { State = GetCurrentState() };
      StateUpdated?.Invoke(this, e);
    }

    private IndexingServerState GetCurrentState() {
      return new IndexingServerState {
        Status = GetStatus(),
        LastIndexUpdateUtc = _lastUpdateUtc,
      };
    }

    private enum IndexingServerPauseReason {
      UserRequest,
      FileWatchBufferOverflow,
    }
  }
}