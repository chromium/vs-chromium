// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Threads {
  public class TaskQueue : ITaskQueue {
    private readonly string _description;
    private readonly ICustomThreadPool _customThreadPool;
    private readonly object _lock = new object();
    private readonly Queue<Entry> _tasks = new Queue<Entry>();

    public TaskQueue(string description, ICustomThreadPool customThreadPool) {
      _description = description;
      _customThreadPool = customThreadPool;
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
      Logger.Log("Queue \"{0}\": Enqueing task \"{1}\"", _description, entry.Description);

      bool isFirstTask;
      lock (_lock) {
        _tasks.Enqueue(entry);
        isFirstTask = (_tasks.Count == 1);
      }

      if (isFirstTask)
        RunTaskAsync(entry);
    }

    private void OnTaskFinished() {
      Entry previous;
      Entry entry = null;
      lock (_lock) {
        // Dequeue the current task...
        previous = _tasks.Dequeue();

        // Are there other tasks?
        if (_tasks.Count > 0) {
          entry = _tasks.Peek();
        }
      }

      Logger.Log("Queue \"{0}\": Executed task \"{1}\"", _description, previous.Description);

      if (entry != null)
        RunTaskAsync(entry);
    }

    private void RunTaskAsync(Entry entry) {
      Logger.Log("Queue \"{0}\": Executing task \"{1}\"", _description, entry.Description);
      _customThreadPool.RunAsync(entry.Task);
    }

    private class Entry {
      public string Description { get; set; }
      public Action Task { get; set; }
    }
  }
}
