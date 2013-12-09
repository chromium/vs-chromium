// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace VsChromiumServer.Threads {
  public class ThreadPool {
    private readonly int _capacity;
    private readonly object _lock = new object();
    private readonly AutoResetEvent _threadReleasedEvent = new AutoResetEvent(false);
    private readonly List<ThreadObject> _threads = new List<ThreadObject>();

    public ThreadPool(int capacity) {
      this._capacity = capacity;
      this._threads.AddRange(Enumerable.Range(0, capacity).Select(i => new ThreadObject(i, this)));
    }

    public int Capacity {
      get {
        return this._capacity;
      }
    }

    public ThreadObject AcquireThread() {
      while (true) {
        var threadObject = TryGetThread();
        if (threadObject != null)
          return threadObject;

        this._threadReleasedEvent.WaitOne();
      }
    }

    private ThreadObject TryGetThread() {
      lock (this._lock) {
        if (this._threads.Count == 0)
          return null;

        int index = this._threads.Count - 1;
        var result = this._threads[index];
        this._threads.RemoveAt(index);
        return result;
      }
    }

    public void ReleaseThread(ThreadObject threadObject) {
      lock (this._lock) {
        this._threads.Add(threadObject);
      }
      this._threadReleasedEvent.Set();
    }
  }
}
