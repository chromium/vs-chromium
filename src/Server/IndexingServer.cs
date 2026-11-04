// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using VsChromium.Core.Threads;
using VsChromium.Server.Cache;
using VsChromium.Server.FileSystem;
using VsChromium.Server.Operations;
using VsChromium.Server.Search;
using VsChromium.Server.Threads;

namespace VsChromium.Server {
  public class IndexingServer : IIndexingServer {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IFileSystemSnapshotManager _fileSystemSnapshotManager;
    private readonly ITaskQueue _stateChangeTaskQueue;
    private readonly IIndexCache _indexCache;

    private bool _paused;
    private bool _indexing;
    private bool _pausedDueToError;
    /// <summary>
    /// True from the publication of a tree of the index cache until the load of the verified tree.
    /// </summary>
    private bool _verifying;
    private DateTime _lastIndexUpdateUtc = DateTime.MinValue;
    /// <summary>
    /// Note: Updated in the processing thread, read from any thread.
    /// </summary>
    private IndexingServerState _currentState;

    public IndexingServer(
      IDateTimeProvider dateTimeProvider,
      IFileSystemSnapshotManager fileSystemSnapshotManager,
      ITaskQueueFactory taskQueueFactory,
      ISearchEngine searchEngine,
      IIndexCache indexCache = null) {
      _dateTimeProvider = dateTimeProvider;
      _indexCache = indexCache;
      _fileSystemSnapshotManager = fileSystemSnapshotManager;
      _stateChangeTaskQueue = taskQueueFactory.CreateQueue("IndexingServer State Change Task Queue");

      _fileSystemSnapshotManager.SnapshotScanStarted += FileSystemSnapshotManagerOnSnapshotScanStarted;
      _fileSystemSnapshotManager.SnapshotScanFinished += FileSystemSnapshotManagerOnSnapshotScanFinished;
      _fileSystemSnapshotManager.FileSystemWatchPaused += FileSystemSnapshotManagerOnFileSystemWatchPaused;
      _fileSystemSnapshotManager.FileSystemWatchResumed += FileSystemSnapshotManagerOnFileSystemWatchResumed;

      searchEngine.FilesLoading += SearchEngineOnFilesLoading;
      searchEngine.FilesLoaded += SearchEngineOnFilesLoaded;
      _currentState = GetCurrentState();
    }

    public IndexingServerState CurrentState => _currentState;

    private IndexingServerStatus GetStatus() {
      // Note: The verification of the tree of the index cache is a scan and a load ("busy").
      if (_verifying && !_paused)
        return IndexingServerStatus.Verifying;
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
        // "Rebuild index": the cache of the projects is deleted, so that the full load reads all
        // the files from disk (e.g. files modified without changing their time and size).
        _indexCache?.Invalidate(_fileSystemSnapshotManager.CurrentSnapshot.ProjectRoots.Select(x => x.Project.RootPath).ToList());
        _fileSystemSnapshotManager.Refresh();
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
      // Note: The task doesn't reference the snapshots (see SearchEngine).
      var fromCache = snapshotScanResult.NewSnapshot != null && snapshotScanResult.NewSnapshot.IsFromCache;
      _stateChangeTaskQueue.ExecuteAsync(token => {
        _indexing = false;
        if (fromCache) {
          _verifying = true;
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
          _lastIndexUpdateUtc = _dateTimeProvider.UtcNow;
          _verifying = filesLoadedResult.FromCache;
        }
        OnStatusUpdated();
      });
    }

    private void FileSystemSnapshotManagerOnFileSystemWatchPaused(object sender, FileSystemWatchPausedEventArgs e) {
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
      _currentState = GetCurrentState();
      var e = new IndexingServerStateUpdatedEventArgs { State = _currentState };
      StateUpdated?.Invoke(this, e);
    }

    private IndexingServerState GetCurrentState() {
      return new IndexingServerState {
        Status = GetStatus(),
        LastIndexUpdateUtc = _lastIndexUpdateUtc,
      };
    }

    private enum IndexingServerPauseReason {
      UserRequest,
      FileWatchBufferOverflow,
    }
  }
}
