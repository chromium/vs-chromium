// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Threads {
  public class ThreadObject {
    private readonly int _id;
    private readonly AutoResetEvent _taskAvailable = new AutoResetEvent(false);
    private readonly ThreadPool _threadPool;
    private Action _currentTask = null;
    private Thread _thread;

    public ThreadObject(int id, ThreadPool threadPool) {
      _id = id;
      _threadPool = threadPool;
    }

    private void Loop() {
      while (true) {
        _taskAvailable.WaitOne();
        try {
          _currentTask();
        }
        catch (Exception e) {
          // TODO(rpaquay): Do we want to propage the exception here?
          Logger.LogException(e, "Error executing task on custom thread pool.");
        }
      }
    }

    /// <summary>
    /// Note: This method does not need any locking/thread safety guard, because a thread object will ever only
    ///  be used by one thread at a time.
    /// </summary>
    public void RunAsync(Action task) {
      if (_thread == null) {
        _thread = new Thread(Loop);
        _thread.Priority = ThreadPriority.AboveNormal;
        _thread.Name = String.Format("CustomThread #{0}", _id);
        _thread.IsBackground = true;
        _thread.Start();
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
