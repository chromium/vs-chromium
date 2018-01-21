// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using VsChromium.Core.Collections;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    /// <summary>
    /// Record the last 100 change notification, for debugging purpose only.
    /// </summary>
    private static readonly PathChangeRecorder GlobalChangeRecorder = new PathChangeRecorder();
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TimeSpan? _autoRestartDelay;
    private readonly TimeSpan _autoRestartObservePeriod = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Buffer for file change notifications. The buffer has its own lock to make sure
    /// watcher event are processed as fast as possible without any lock contention,
    /// to prevent internal buffer overflow exceptions (from watchers) as much as possible.
    /// </summary>
    private readonly ConcurrentBufferQueue<Action> _watcherEventQueue = new ConcurrentBufferQueue<Action>();

    /// <summary>
    /// Our current state which changes when we are started, stopped, run into errors with file system watcher, etc.
    /// This state allows reacting to external operations and events specifically for each state.
    /// </summary>
    private State _state;

    /// <summary>
    /// The lock to protect access to <see cref="_state"/>
    /// </summary>
    private readonly object _stateLock = new object();

    public DirectoryChangeWatcher(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider, TimeSpan? autoRestartDelay) {
      _fileSystem = fileSystem;
      _dateTimeProvider = dateTimeProvider;
      _autoRestartDelay = autoRestartDelay;
      _state = new RunningState(new StateHost(this));
      _state.StateHost.PollingThread.Polling += PollingThreadOnPolling;
    }

    public void WatchDirectories(IEnumerable<FullPath> directories) {
      lock (_stateLock) {
        _state = _state.OnWatchDirectories(directories);
        _state.OnStateActive();
      }
    }

    public void Pause() {
      lock (_stateLock) {
        _state = _state.OnPause();
        _state.OnStateActive();
      }
    }

    public void Resume() {
      lock (_stateLock) {
        _state = _state.OnResume();
        _state.OnStateActive();
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;
    public event Action<Exception> Error;
    public event Action Paused;
    public event Action Resumed;

    protected virtual void OnError(Exception obj) {
      Error?.Invoke(obj);
    }

    protected virtual void OnPathsChanged(IList<PathChangeEntry> changes) {
      PathsChanged?.Invoke(changes);
    }

    protected virtual void OnPaused() {
      Paused?.Invoke();
    }

    protected virtual void OnResumed() {
      Resumed?.Invoke();
    }

    private void PollingThreadOnPolling(object sender, EventArgs eventArgs) {
      lock (_stateLock) {
        _state = _state.OnPolling();
        _state.OnStateActive();
      }

      var actions = _watcherEventQueue.DequeueAll();
      if (actions.Count > 0) {
        Logger.LogDebug("DirectectoryChangeWatcher: Processing {0} file change event(s)", actions.Count);
        foreach (var action in actions) {
          Logger.WrapActionInvocation(() => {
            action();
          });
        }
      }
    }

    private void WatcherOnError(object sender, ErrorEventArgs args) {
      // Note: Unlike other type of watcher events, errors are not queued,
      // instead they are processed directly, after clearing the queue. This
      // is because we know that such events are not useful after errors, since
      // the state of the file system is not known.
      Logger.WrapActionInvocation(() => {
        var actions = _watcherEventQueue.DequeueAll();
        if (actions.Count > 0) {
          Logger.LogInfo("DirectectoryChangeWatcher: Ignoring {0} file change events due to overflow error", actions.Count);
        }

        lock (_stateLock) {
          _state = _state.OnWatcherErrorEvent(sender, args);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileChangedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileCreatedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileDeletedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileRenamedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static void LogPathForDebugging(string path, PathChangeKind kind, PathKind pathKind) {
#if false
      var pathToLog = @"";
      if (SystemPathComparer.Instance.IndexOf(path, pathToLog, 0, path.Length) == 0) {
        Logger.LogInfo("*************************** {0}: {1}-{2} *******************", path, kind, pathKind);
      }
#endif
    }
  }
}
