// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore;

namespace VsChromiumServer.Threads {
  public class TaskQueue : ITaskQueue {
    private readonly ICustomThreadPool _customThreadPool;
    private readonly object _lock = new object();
    private readonly Queue<Entry> _tasks = new Queue<Entry>();

    public TaskQueue(ICustomThreadPool customThreadPool) {
      this._customThreadPool = customThreadPool;
    }

    public void Enqueue(string description, Action task) {
      var entry = new Entry {
        Description = description,
        Task = () => {
          try {
            task();
          }
          finally {
            OnTaskFinished();
          }
        }
      };
      Logger.Log("Enqueing task: {0}", entry.Description);

      bool isFirstTask;
      lock (this._lock) {
        this._tasks.Enqueue(entry);
        isFirstTask = (this._tasks.Count == 1);
      }

      if (isFirstTask)
        RunTaskAsync(entry);
    }

    private void OnTaskFinished() {
      Entry previous;
      Entry entry = null;
      lock (this._lock) {
        // Dequeue the current task...
        previous = this._tasks.Dequeue();

        // Are there other tasks?
        if (this._tasks.Count > 0) {
          entry = this._tasks.Peek();
        }
      }

      Logger.Log("Executed task: {0}", previous.Description);

      if (entry != null)
        RunTaskAsync(entry);
    }

    private void RunTaskAsync(Entry entry) {
      Logger.Log("Running task: {0}", entry.Description);
      this._customThreadPool.RunAsync(entry.Task);
    }

    private class Entry {
      public string Description { get; set; }
      public Action Task { get; set; }
    }
  }
}
