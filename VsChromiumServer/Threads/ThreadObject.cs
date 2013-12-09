// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromiumCore;

namespace VsChromiumServer.Threads {
  public class ThreadObject {
    private readonly int _id;
    private readonly AutoResetEvent _taskAvailable = new AutoResetEvent(false);
    private readonly ThreadPool _threadPool;
    private Action _currentTask = null;
    private Thread _thread;

    public ThreadObject(int id, ThreadPool threadPool) {
      this._id = id;
      this._threadPool = threadPool;
    }

    private void Loop() {
      while (true) {
        this._taskAvailable.WaitOne();
        try {
          this._currentTask();
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
      if (this._thread == null) {
        this._thread = new Thread(Loop);
        this._thread.Priority = ThreadPriority.AboveNormal;
        this._thread.Name = String.Format("CustomThread #{0}", this._id);
        this._thread.IsBackground = true;
        this._thread.Start();
      }

      this._currentTask = task;
      this._taskAvailable.Set();
    }

    /// <summary>
    /// Note: This method does not need any locking/thread safety guard, because a thread object will ever only
    ///  be used by one thread at a time.
    /// </summary>
    public void Release() {
      this._currentTask = null;
      this._threadPool.ReleaseThread(this);
    }
  }
}
