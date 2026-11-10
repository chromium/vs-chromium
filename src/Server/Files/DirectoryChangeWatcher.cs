// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    // Note: After an error (e.g. buffer overflow of the OS notifications), the watcher restarts
    // and the index is rescanned once this period passed (changes made in the meantime are found by
    // the rescan).
    private readonly TimeSpan _autoRestartObservePeriod = TimeSpan.FromSeconds(2);

    /// <summary>
    /// When change notifications are not available (see <see cref="PollingFallbackState"/>), the
    /// period of the <see cref="RescanRequired"/> events.
    /// </summary>
    public TimeSpan PollingFallbackPeriod { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// The delay without new change after which the changes are posted (debouncing), for all the
    /// kinds of changes; null for the default delays (0.5 s for file changes, 1 s for creations and
    /// deletions). Benchmarks only ("--watcher-delay-ms"). Set before the watcher starts.
    /// </summary>
    public TimeSpan? ChangeDelay { get; set; }

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

    private void TransitionState(Func<State, State> transition) {
      var previous = _state;
      var next = transition(previous);
      // If a synchronous callback (such as Error during Start on Linux) already transitioned
      // _state re-entrantly while inside transition(previous), preserve that state transition.
      if (!ReferenceEquals(_state, previous)) {
        return;
      }
      if (!ReferenceEquals(previous, next)) {
        _state = next;
        _state.OnStateActive();
      }
    }

    public void WatchDirectories(IEnumerable<FullPath> directories) {
      lock (_stateLock) {
        TransitionState(s => s.OnWatchDirectories(directories));
      }
    }

    public void Pause() {
      lock (_stateLock) {
        TransitionState(s => s.OnPause());
      }
    }

    public void Resume() {
      lock (_stateLock) {
        TransitionState(s => s.OnResume());
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;
    public event Action<Exception> Error;
    public event Action Paused;
    public event Action Resumed;
    public event Action RescanRequired;

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

    protected virtual void OnRescanRequired() {
      RescanRequired?.Invoke();
    }

    /// <summary>
    /// Returns <code>true</code> if <paramref name="exception"/> means that file system watchers
    /// cannot be created because of an operating system resource limit (e.g. the inotify watch or
    /// instance limits on Linux), in which case retrying is pointless.
    /// </summary>
    public static bool IsWatchLimitError(Exception exception) {
      for (var e = exception; e != null; e = e.InnerException) {
        if (e is IOException &&
            e.Message != null &&
            (e.Message.IndexOf("inotify", StringComparison.OrdinalIgnoreCase) >= 0 ||
             e.Message.IndexOf("user limit", StringComparison.OrdinalIgnoreCase) >= 0 ||
             e.Message.IndexOf("No space left on device", StringComparison.OrdinalIgnoreCase) >= 0 ||
             e.Message.IndexOf("Too many open files", StringComparison.OrdinalIgnoreCase) >= 0)) {
          return true;
        }
        if (!OperatingSystem.IsWindows() && e is Win32Exception w32 &&
            (w32.NativeErrorCode == 28 /* ENOSPC */ || w32.NativeErrorCode == 24 /* EMFILE */ || w32.NativeErrorCode == 23 /* ENFILE */)) {
          return true;
        }
      }
      return false;
    }

    private void PollingThreadOnPolling(object sender, EventArgs eventArgs) {
      lock (_stateLock) {
        TransitionState(s => s.OnPolling());
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
          TransitionState(s => s.OnWatcherErrorEvent(sender, args));
        }
      });
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          TransitionState(s => s.OnWatcherFileChangedEvent(sender, args, pathKind));
        }
      });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          TransitionState(s => s.OnWatcherFileCreatedEvent(sender, args, pathKind));
        }
      });
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          TransitionState(s => s.OnWatcherFileDeletedEvent(sender, args, pathKind));
        }
      });
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs args, PathKind pathKind) {
      _watcherEventQueue.Enqueue(() => {
        lock (_stateLock) {
          TransitionState(s => s.OnWatcherFileRenamedEvent(sender, args, pathKind));
        }
      });
    }
  }
}
