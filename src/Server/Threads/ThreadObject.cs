// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  public class ThreadObject {
    private readonly ThreadPool _threadPool;
    private readonly int _id;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AutoResetEvent _taskAvailable = new AutoResetEvent(false);
    private Action _currentTask = null;
    private Thread _thread;
    private readonly object _threadLock = new object();

    public ThreadObject(ThreadPool threadPool, int id, IDateTimeProvider dateTimeProvider) {
      _dateTimeProvider = dateTimeProvider;
      _id = id;
      _threadPool = threadPool;
    }

    private void ThreadLoop() {
      lock (_threadLock) {
        Debug.Assert(_thread != null);
        Debug.Assert(Thread.CurrentThread == _thread);
      }
      while (true) {
        bool signaled = _taskAvailable.WaitOne(TimeSpan.FromSeconds(5));
        if (!signaled) {
          // Exit thread, it's been idle for 5 seconds
          break;
        }
        try {
          _currentTask();
        }
        catch (Exception e) {
          // TODO(rpaquay): Do we want to propage the exception here?
          Logger.LogError(e, "Error executing task on custom thread pool.");
        }
        finally {
          _currentTask = null;
        }
      }
      // Exit thread
      lock (_threadLock) {
        _thread = null;
      }
    }

    /// <summary>
    /// Note: This method does not need any locking/thread safety guard, because a thread object will ever only
    ///  be used by one thread at a time.
    /// </summary>
    public void RunAsync(Action task) {
      if (_thread == null) {
        lock (_threadLock) {
          if (_thread == null) {
            _thread = new Thread(ThreadLoop);
            _thread.Priority = ThreadPriority.AboveNormal;
            _thread.Name = String.Format("CustomThread #{0}", _id);
            _thread.IsBackground = true;
            _thread.Start();
          }
        }
      }

      _currentTask = task;
      _taskAvailable.Set();
    }

    /// <summary>
    /// Note: This method does not need any locking/thread safety guard, because a thread object will ever only
    ///  be used by one thread at a time.
    /// </summary>
    public void Release() {
      _currentTask = null;
      _threadPool.ReleaseThread(this);
    }
  }
}
