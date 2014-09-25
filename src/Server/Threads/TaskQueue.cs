// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  public class TaskQueue : ITaskQueue {
    private readonly string _description;
    private readonly ICustomThreadPool _customThreadPool;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly Queue<TaskEntry> _tasks = new Queue<TaskEntry>();
    private readonly object _lock = new object();

    public TaskQueue(string description, ICustomThreadPool customThreadPool, IDateTimeProvider dateTimeProvider) {
      _description = description;
      _customThreadPool = customThreadPool;
      _dateTimeProvider = dateTimeProvider;
    }

    public void Enqueue(string description, Action task) {
      var entry = new TaskEntry {
        Description = description,
        EnqueuedDateTimeUtc = _dateTimeProvider.UtcNow,
        Action = task,
        StopWatch = new Stopwatch(),
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

    private void RunTaskAsync(TaskEntry entry) {
      _customThreadPool.RunAsync(() => {
        try {
          Logger.Log("Queue \"{0}\": Executing task \"{1}\" after waiting for {2:n0} msec",
            _description,
            entry.Description,
            (_dateTimeProvider.UtcNow - entry.EnqueuedDateTimeUtc).TotalMilliseconds);
          entry.StopWatch.Start();
          entry.Action();
        }
        finally {
          OnTaskFinished(entry);
        }
      });
    }

    private void OnTaskFinished(TaskEntry entry) {
      entry.StopWatch.Stop();
      Logger.Log("Queue \"{0}\": Executed task \"{1}\" in {2:n0} msec", 
        _description,
        entry.Description,
        entry.StopWatch.ElapsedMilliseconds);

      TaskEntry nextEntry = null;
      lock (_lock) {
        // Dequeue the current task...
        TaskEntry previousEntry = _tasks.Dequeue();
        Debug.Assert(object.ReferenceEquals(previousEntry, entry));

        // Are there other tasks?
        if (_tasks.Count > 0) {
          nextEntry = _tasks.Peek();
        }
      }


      if (nextEntry != null)
        RunTaskAsync(nextEntry);
    }

    private class TaskEntry {
      public string Description { get; set; }
      public Action Action { get; set; }
      public DateTime EnqueuedDateTimeUtc { get; set; }
      public Stopwatch StopWatch { get; set; }
    }
  }
}
