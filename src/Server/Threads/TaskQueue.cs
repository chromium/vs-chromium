// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  public class TaskQueue : ITaskQueue {
    private readonly string _description;
    private readonly ICustomThreadPool _customThreadPool;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TaskEntryQueue _tasks = new TaskEntryQueue();
    private readonly CancellationTokenTracker _taskCancellationTracker = new CancellationTokenTracker();
    private volatile TaskEntry _runningTask;
    private readonly object _lock = new object();

    public TaskQueue(string description, ICustomThreadPool customThreadPool, IDateTimeProvider dateTimeProvider) {
      _description = description;
      _customThreadPool = customThreadPool;
      _dateTimeProvider = dateTimeProvider;
    }

    public void Enqueue(TaskId id, Action<CancellationToken> task) {
      var entry = new TaskEntry {
        Id = id,
        EnqueuedDateTimeUtc = _dateTimeProvider.UtcNow,
        Action = task,
        StopWatch = new Stopwatch(),
      };

      Logger.LogInfo("Queue \"{0}\": Enqueuing task \"{1}\"", _description, entry.Id.Description);

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

    public void EnqueueUnique(Action<CancellationToken> task) {
      Enqueue(new TaskId("UniqueTask"), task);
    }

    public void CancelCurrentTask() {
      _taskCancellationTracker.CancelCurrent();
    }

    public void CancelAll() {
      lock (_lock) {
        _tasks.Clear();
      }
      _taskCancellationTracker.CancelCurrent();
    }

    private void RunTaskAsync(TaskEntry entry) {
      _customThreadPool.RunAsync(() => {
        try {
          Logger.LogInfo("Queue \"{0}\": Executing task \"{1}\" after waiting for {2:n0} msec",
            _description,
            entry.Id.Description,
            (_dateTimeProvider.UtcNow - entry.EnqueuedDateTimeUtc).TotalMilliseconds);
          entry.StopWatch.Start();
          entry.Action(_taskCancellationTracker.NewToken());
        }
        finally {
          OnTaskFinished(entry);
        }
      });
    }

    private void OnTaskFinished(TaskEntry task) {
      task.StopWatch.Stop();
      Logger.LogInfo("Queue \"{0}\": Executed task \"{1}\" in {2:n0} msec",
        _description,
        task.Id.Description,
        task.StopWatch.ElapsedMilliseconds);

      TaskEntry nextTask;
      lock (_lock) {
        Debug.Assert(ReferenceEquals(_runningTask, task));
        nextTask = _runningTask = _tasks.Dequeue();
      }

      if (nextTask != null)
        RunTaskAsync(nextTask);
    }

    private class TaskEntryQueue {
      private readonly Dictionary<object, LinkedListNode<TaskEntry>> _map =
        new Dictionary<object, LinkedListNode<TaskEntry>>();

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

      public void Clear() {
        _queue.Clear();
        _map.Clear();
      }
    }

    private class TaskEntry {
      public TaskId Id { get; set; }
      public Action<CancellationToken> Action { get; set; }
      public DateTime EnqueuedDateTimeUtc { get; set; }
      public Stopwatch StopWatch { get; set; }
    }
  }
}
