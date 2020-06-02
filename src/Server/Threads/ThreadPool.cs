// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  public class ThreadConcurrentBag {
    private readonly int _capacity;
    private readonly object _lock = new object();
    private readonly List<ThreadObject> _threads = new List<ThreadObject>();

    public ThreadConcurrentBag(IDateTimeProvider dateTimeProvider, int capacity) {
      _capacity = capacity;
      _threads.AddRange(Enumerable.Range(0, capacity).Select(i => new ThreadObject(i, dateTimeProvider)));
    }

    public int Capacity { get { return _capacity; } }

    public ThreadObject TryAcquireThread() {
      lock (_lock) {
        if (_threads.Count == 0)
          return null;

        int index = _threads.Count - 1;
        var result = _threads[index];
        _threads.RemoveAt(index);
        return result;
      }
    }

    public void ReleaseThread(ThreadObject threadObject) {
      lock (_lock) {
        _threads.Add(threadObject);
      }
    }
  }
}
