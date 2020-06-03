// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Server.Threads {
  [Export(typeof(ICustomThreadPool))]
  public class CustomThreadPool : ICustomThreadPool {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly object _lock = new object();
    private readonly BlockingCollection<Action> _taskQueue = new BlockingCollection<Action>();
    private readonly List<ThreadPoolEntry> _threadPool;
    private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

    [ImportingConstructor]
    public CustomThreadPool(IDateTimeProvider dateTimeProvider)
      : this(dateTimeProvider, 5) {
    }

    public CustomThreadPool(IDateTimeProvider dateTimeProvider, int capacity) {
      _dateTimeProvider = dateTimeProvider;
      _threadPool = new List<ThreadPoolEntry>();
      _threadPool.AddRange(Enumerable.Range(0, capacity).Select(i => new ThreadPoolEntry(i, _taskQueue, _tokenSource.Token)));

      // Start threads right away
      _threadPool.ForEach(x => x.Start());
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
        _taskQueue.Add(task);
      }
    }

    private class ThreadPoolEntry {
      private readonly int _id;
      private readonly Thread _thread;
      private readonly BlockingCollection<Action> _queue;
      private readonly CancellationToken _token;

      public ThreadPoolEntry(int id, BlockingCollection<Action> queue, CancellationToken token) {
        _id = id;
        _thread = new Thread(ThreadLoop);
        _queue = queue;
        _token = token;
      }

      public void Start() {
        _thread.Priority = ThreadPriority.AboveNormal;
        _thread.Name = String.Format("CustomThread #{0}", _id);
        _thread.IsBackground = true;
        _thread.Start();
      }

      private void ThreadLoop() {
        try {
          foreach (var task in _queue.GetConsumingEnumerable(_token)) {
            try {
              task();
            } catch (Exception e) {
              // TODO(rpaquay): Do we want to propage the exception here?
              Logger.LogError(e, "Error executing task on custom thread pool, moving on to next task");
            }
          }
        } catch(OperationCanceledException e) {
          Logger.LogInfo(e, "Queue has been cancelled, terminating thread");
        } catch(Exception e) {
          Logger.LogError(e, "Error consuming tasks from queue, terminating thread");
        }
      }
    }
  }
}
