// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading.Tasks;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  [Export(typeof(ICustomThreadPool))]
  public class CustomThreadPool : ICustomThreadPool {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly object _lock = new object();
    private readonly ThreadPool _threadPool;
    private readonly object _queueLock = new object();
    private readonly Queue<Action> _taskQueue = new Queue<Action>();

    [ImportingConstructor]
    public CustomThreadPool(IDateTimeProvider dateTimeProvider)
      : this(dateTimeProvider, 10) {
    }

    public CustomThreadPool(IDateTimeProvider dateTimeProvider, int capacity) {
      _dateTimeProvider = dateTimeProvider;
      _threadPool = new ThreadPool(dateTimeProvider, capacity);
    }

    public void RunAsync(Action task) {
      RunAsync(task, TimeSpan.Zero);
    }

    public void RunAsync(Action task, TimeSpan delay) {
      if (delay > TimeSpan.Zero) {
        // Call ourselves after delay
        // Note: This is a "cheap" way of delaying execution without blocking a thread.
        //       The caveat is that this is done with the default TaskScheduler, meaning
        //       The task may be delayed even more if lots of tasks are already running.
        //       Given that the caller ask for a delay, it seems reasonable compromise.
        //       The other option would have been to write custom with some sort of
        //       timer usage.
        Task.Delay(delay).ContinueWith(_ => RunAsync(task, TimeSpan.Zero));
      } else {
        // Enqueue
        lock (_queueLock) {
          _taskQueue.Enqueue(task);
        }

        // Process queue if any available thread
        ProcessQueueAsync();
      }
    }

    private void ProcessQueueAsync() {
      var thread = TryAcquireThread();
      if (thread != null) {
        thread.RunAsync(() => ProcessQueueAndReleaseThread(thread));
      }
    }

    private void ProcessQueueAndReleaseThread(ThreadObject thread) {
      try {
        ProcessQueue();
      } finally {
        ReleaseThread(thread);
      }

      // There may have been more items enqueued concurrently: Mote tasks may have been
      // enueue we may have been
      // releasing the thread while
      // schedule another queue processing if needed
      bool queueIsEmpty;
      lock (_queueLock) {
        queueIsEmpty = _taskQueue.Count == 0;
      }
      if (!queueIsEmpty) {
        ProcessQueueAsync();
      }
    }

    private void ProcessQueue() {
      // Process all items in the queue. This happens concurrently on
      // all active threads of the thread pool.
      while (true) {
        Action task = TryGetTaskFromQueue();

        // Queue is empty, bail
        if (task == null) {
          break;
        }

        try {
          task();
        } catch (Exception e) {
          // TODO(rpaquay): Do we want to propage the exception here?
          Logger.LogError(e, "Error executing task on custom thread pool.");
        }
      }
    }

    private Action TryGetTaskFromQueue() {
      lock (_queueLock) {
        return (_taskQueue.Count == 0) ? null : _taskQueue.Dequeue();
      }
    }

    private ThreadObject TryAcquireThread() {
      return _threadPool.TryAcquireThread();
    }

    private void ReleaseThread(ThreadObject thread) {
      thread.Release();
    }
  }
}
