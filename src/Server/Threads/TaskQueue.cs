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
    private readonly TaskEntryQueue _taskQueue = new TaskEntryQueue();
    private readonly object _lock = new object();
    private readonly CancellationTokenTracker _taskCancellationTracker = new CancellationTokenTracker();
    private volatile TaskEntry _runningTask;

    public TaskQueue(string description, ICustomThreadPool customThreadPool, IDateTimeProvider dateTimeProvider) {
      _description = description;
      _customThreadPool = customThreadPool;
      _dateTimeProvider = dateTimeProvider;
    }

    public void Enqueue(TaskId id, Action<CancellationToken> task) {
      Enqueue(id, task, TimeSpan.Zero);
    }

    public void Enqueue(TaskId id, Action<CancellationToken> task, TimeSpan delay) {
      var entry = new TaskEntry {
        Id = id,
        Action = task,
        Delay = delay,
        EnqueuedDateTimeUtc = _dateTimeProvider.UtcNow,
        StopWatch = new Stopwatch(),
      };

      Logger.LogDebug("Queue \"{0}\": Enqueuing task \"{1}\"", _description, entry.Id.Description);

      lock (_lock) {
        _taskQueue.Enqueue(entry);
      }

      RunNextTaskIfAvailableAsync(null);
    }

    public void CancelCurrentTask() {
      _taskCancellationTracker.CancelCurrent();
    }

    public void CancelAll() {
      lock (_lock) {
        _taskQueue.Clear();
      }
      _taskCancellationTracker.CancelCurrent();
    }

    private void RunTaskAsync(TaskEntry task) {
      Invariants.Assert(ReferenceEquals(_runningTask, task));
      _customThreadPool.RunAsync(() => {
        Invariants.Assert(ReferenceEquals(_runningTask, task));
        try {
          if (Logger.IsDebugEnabled) {
            Logger.LogDebug("Queue \"{0}\": Executing task \"{1}\" after waiting for {2:n0} msec",
              _description,
              task.Id.Description,
              (_dateTimeProvider.UtcNow - task.EnqueuedDateTimeUtc).TotalMilliseconds);
          }

          task.StopWatch.Start();
          task.Action(_taskCancellationTracker.NewToken());
        } finally {
          Invariants.Assert(ReferenceEquals(_runningTask, task));
          task.StopWatch.Stop();
          if (Logger.IsDebugEnabled) {
            Logger.LogDebug("Queue \"{0}\": Executed task \"{1}\" in {2:n0} msec",
              _description,
              task.Id.Description,
              task.StopWatch.ElapsedMilliseconds);
          }
          RunNextTaskIfAvailableAsync(task);
        }
      });
    }

    private void RunNextTaskIfAvailableAsync(TaskEntry task) {
      TaskEntryQueue.DequeueResult queueEntry;
      lock (_lock) {
        if (task == null) {
          // If there is a running task, bail, because we will be called again when the running task
          // finishes.
          if (_runningTask != null) {
            return;
          }
        } else {
          Invariants.Assert(ReferenceEquals(_runningTask, task));
        }
        queueEntry = _taskQueue.Dequeue(_dateTimeProvider.UtcNow);
        _runningTask = queueEntry.TaskEntry; // May be null if only pdelayed tasks
      }

      if (queueEntry.TaskEntry != null) {
        // If there is a task available, run it
        RunTaskAsync(queueEntry.TaskEntry);
      } else if (queueEntry.HasPending) {
        // Run this method in a little while if there are pending tasks
        _customThreadPool.RunAsync(() => RunNextTaskIfAvailableAsync(null), TimeSpan.FromMilliseconds(50));
      }
    }

    /// <summary>
    /// A collection of <see cref="TaskEntry"/> that behaves like a queue
    /// for <see cref="Enqueue(TaskEntry)"/> and <see cref="Dequeue"/>,
    /// operations, but also ensures that there is only one <see cref="TaskEntry"/>
    /// value per <see cref="TaskId"/> key.
    /// </summary>
    private class TaskEntryQueue
    {
      private readonly Dictionary<TaskId, LinkedListNode<TaskEntry>> _map =
        new Dictionary<TaskId, LinkedListNode<TaskEntry>>();

      private readonly LinkedList<TaskEntry> _queue = new LinkedList<TaskEntry>();

      public bool IsEmpty { get { return _queue.Count == 0; } }

      public void Enqueue(TaskEntry entry) {
        LinkedListNode<TaskEntry> currentEntry;
        if (_map.TryGetValue(entry.Id, out currentEntry)) {
          _queue.Remove(currentEntry);
          _map.Remove(entry.Id);
        }
        var newEntry = _queue.AddLast(entry);
        _map.Add(entry.Id, newEntry);
      }

      public struct DequeueResult
      {
        public bool HasPending;
        public TaskEntry TaskEntry;
      }

      public DequeueResult Dequeue(DateTime utcNow) {
        for (var node = _queue.First; node != null; node = node.Next) {
          var entry = node.Value;
          if ((entry.Delay <= TimeSpan.Zero) || ((utcNow - entry.EnqueuedDateTimeUtc) >= entry.Delay)) {
            _queue.Remove(node);
            _map.Remove(entry.Id);
            return new DequeueResult() {
              HasPending = _queue.Count > 0,
              TaskEntry = entry,
            };
          }
        }
        return new DequeueResult() {
          HasPending = _queue.Count > 0
        };
      }

      public void Clear() {
        _queue.Clear();
        _map.Clear();
      }
    }

    private class TaskEntry
    {
      public TaskId Id { get; set; }
      public Action<CancellationToken> Action { get; set; }
      public TimeSpan Delay { get; set; }
      public DateTime EnqueuedDateTimeUtc { get; set; }
      public Stopwatch StopWatch { get; set; }
    }
  }
}
