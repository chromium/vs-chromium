// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class StateHost {
      private readonly DirectoryChangeWatcher _parentWatcher;
      private readonly BoundedOperationLimiter _logLimiter = new BoundedOperationLimiter(10);
      private readonly PollingWatcherThread _pollingThread;
      /// <summary>
      /// Dictionary of watchers, one per root directory path.
      /// </summary>
      private readonly Dictionary<FullPath, DirectoryWatcherhEntry> _watcherDictionary = new Dictionary<FullPath, DirectoryWatcherhEntry>();

      public StateHost(DirectoryChangeWatcher parentWatcher) {
        _parentWatcher = parentWatcher;
        _pollingThread = new PollingWatcherThread();
      }

      public DirectoryChangeWatcher ParentWatcher {
        get { return _parentWatcher; }
      }

      public BoundedOperationLimiter LogLimiter {
        get { return _logLimiter; }
      }

      public PollingWatcherThread PollingThread {
        get { return _pollingThread; }
      }

      /// <summary>
      /// Dictionary of watchers, one per root directory path.
      /// </summary>
      public Dictionary<FullPath, DirectoryWatcherhEntry> WatcherDictionary {
        get { return _watcherDictionary; }
      }

      public class PollingWatcherThread {
        private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
        /// <summary>
        /// The polling and event posting thread.
        /// </summary>
        private readonly TimeSpan _pollingThreadPeriod = TimeSpan.FromSeconds(1.0);
        private Thread _thread;

        public void Start() {
          if (_thread == null) {
            _thread = new Thread(ThreadLoop) { IsBackground = true };
            _thread.Start();
          }
        }

        public void WakeUp() {
          _eventReceived.Set();
        }

        public event EventHandler Polling;

        private void ThreadLoop() {
          Logger.LogInfo("Starting directory change notification monitoring thread.");
          try {
            while (true) {
              _eventReceived.WaitOne(_pollingThreadPeriod);
              OnPolling();
              //lock (_stateLock) {
              //  _state = _state.OnPolling();
              //  _state.OnStateActive();
              //}
            }
          } catch (Exception e) {
            Logger.LogError(e, "Error in DirectoryChangeWatcher.");
          }
        }

        protected virtual void OnPolling() {
          Polling?.Invoke(this, EventArgs.Empty);
        }

        public bool IsThread(Thread thread) {
          return Equals(_thread, thread);
        }
      }
    }
  }
}