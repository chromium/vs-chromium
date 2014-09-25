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
    private readonly TaskEntryQueue _tasks = new TaskEntryQueue();
    private volatile TaskEntry _runningTask;
    private readonly object _lock = new object();

    public TaskQueue(string description, ICustomThreadPool customThreadPool, IDateTimeProvider dateTimeProvider) {
      _description = description;
      _customThreadPool = customThreadPool;
      _dateTimeProvider = dateTimeProvider;
    }

    public void Enqueue(string description, Action task, object id = null) {
      var entry = new TaskEntry {
        Id = id ?? new object(),
        Description = description,
        EnqueuedDateTimeUtc = _dateTimeProvider.UtcNow,
        Action = task,
        StopWatch = new Stopwatch(),
      };

      Logger.Log("Queue \"{0}\": Enqueing task \"{1}\"", _description, entry.Description);

      bool isFirstTask;
      lock (_lock) {
        if (_runningTask == null) {
          _runningTask = entry;
          isFirstTask = true;
        } else {
          _tasks.Enqueue(entry);
          isFirstTask = false;
        }
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

    private void OnTaskFinished(TaskEntry task) {
      task.StopWatch.Stop();
      Logger.Log("Queue \"{0}\": Executed task \"{1}\" in {2:n0} msec",
        _description,
        task.Description,
        task.StopWatch.ElapsedMilliseconds);

      TaskEntry nextTask = null;
      lock (_lock) {
        Debug.Assert(object.ReferenceEquals(_runningTask, task));
        nextTask = _runningTask = _tasks.Dequeue();
      }

      if (nextTask != null)
        RunTaskAsync(nextTask);
    }

    private class TaskEntryQueue {
      private readonly Dictionary<object, LinkedListNode<TaskEntry>> _map = new Dictionary<object, LinkedListNode<TaskEntry>>();
      private readonly LinkedList<TaskEntry> _queue = new LinkedList<TaskEntry>();

      public void Enqueue(TaskEntry entry) {
        LinkedListNode<TaskEntry> currentEntry;
        if (_map.TryGetValue(entry.Id, out currentEntry)) {
          _queue.Remove(currentEntry);
          _map.Remove(entry.Id);
        }
        var newEntry = _queue.AddLast(entry);
        _map.Add(entry.Id, newEntry);
      }

      public TaskEntry Dequeue() {
        if (_queue.Count == 0)
          return null;

        var result = _queue.First.Value;
        _queue.RemoveFirst();
        _map.Remove(result.Id);
        return result;
      }
    }

    private class TaskEntry {
      public Object Id { get; set; }
      public string Description { get; set; }
      public Action Action { get; set; }
      public DateTime EnqueuedDateTimeUtc { get; set; }
      public Stopwatch StopWatch { get; set; }
    }
  }
}
