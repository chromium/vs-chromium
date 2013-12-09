// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Linq;

namespace VsChromiumServer.Threads {
  [Export(typeof(ICustomThreadPool))]
  public class CustomThreadPool : ICustomThreadPool {
    private readonly object _lock = new object();
    private readonly ThreadPool _threadPool;

    public CustomThreadPool() {
      this._threadPool = new ThreadPool(10);
    }

    public CustomThreadPool(int capacity) {
      this._threadPool = new ThreadPool(capacity);
    }

    public void RunAsync(Action task) {
      var thread = AcquireThread();
      thread.RunAsync(() => ExecuteTaskAndReleaseThread(thread, task));
    }

    public IEnumerable<TDest> RunInParallel<TSource, TDest>(
        IList<TSource> source,
        Func<TSource, TDest> selector,
        CancellationToken token) {
      lock (this._lock) {
        return RunInParallelWorker(source, selector, token);
      }
    }

    private IEnumerable<TDest> RunInParallelWorker<TSource, TDest>(
        IList<TSource> source,
        Func<TSource, TDest> selector,
        CancellationToken token) {
      var partitions = source
          .CreatePartitions(this._threadPool.Capacity)
          .Select(items => new Partition<TSource, TDest> {
            Items = items,
            ThreadObject = null,
            WaitHandle = new ManualResetEvent(false),
            Selector = selector,
            Result = new List<TDest>()
          })
          .ToList();

      try {
        partitions.ForAll(t => t.ThreadObject = AcquireThread());
        partitions.ForAll(t => RunPartitionAsync(t, token));
        partitions.ForAll(t => t.WaitHandle.WaitOne());
        token.ThrowIfCancellationRequested();
        var errors = partitions.Select(x => x.Exception).Where(x => x != null).ToList();
        if (errors.Any()) {
          throw new AggregateException(errors);
        }
        return partitions.SelectMany(t => t.Result);
      }
      finally {
        partitions.ForAll(x => {
          if (x.ThreadObject != null)
            ReleaseThread(x.ThreadObject);
          x.WaitHandle.Dispose();
        });
      }
    }

    private static void RunPartitionAsync<TSource, TDest>(Partition<TSource, TDest> partition, CancellationToken token) {
      partition.ThreadObject.RunAsync(() => {
        try {
          foreach (var item in partition.Items) {
            if (token.IsCancellationRequested)
              break;
            var destItem = partition.Selector(item);
            if (destItem != null)
              partition.Result.Add(destItem);
          }
        }
        catch (Exception e) {
          partition.Exception = e;
        }
        finally {
          partition.WaitHandle.Set();
        }
      });
    }

    private void ExecuteTaskAndReleaseThread(ThreadObject thread, Action task) {
      try {
        task();
      }
      catch (Exception e) {
        // TODO(rpaquay): Do we want to propage the exception here?
        Logger.LogException(e, "Error executing task on custom thread pool.");
      }
      finally {
        ReleaseThread(thread);
      }
    }

    private ThreadObject AcquireThread() {
      return this._threadPool.AcquireThread();
    }

    private void ReleaseThread(ThreadObject thread) {
      thread.Release();
    }

    public class Partition<TSource, TDest> {
      public IList<TSource> Items { get; set; }
      public ThreadObject ThreadObject { get; set; }
      public ManualResetEvent WaitHandle { get; set; }
      public List<TDest> Result { get; set; }
      public Exception Exception { get; set; }
      public Func<TSource, TDest> Selector { get; set; }
    }
  }
}
