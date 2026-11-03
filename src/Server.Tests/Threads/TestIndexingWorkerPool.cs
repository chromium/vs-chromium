// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Threads;

namespace VsChromium.Tests.Threads {
  [TestClass]
  public class TestIndexingWorkerPool {
    [TestMethod]
    public void CoreFirstWakeupAndElasticExpansionWork() {
      using var pool = new IndexingWorkerPool(
        coreThreadCount: 2,
        elasticThreadCount: 4,
        contentionMode: IndexingWorkerPool.ContentionMode.AlwaysUncontended);
      Assert.AreEqual(2, pool.CoreThreadCount);
      Assert.AreEqual(4, pool.ElasticThreadCount);
      Assert.AreEqual(6, pool.TotalThreadCount);

      // Small job (2 batches): wakes the 2 parked Core threads first.
      using var barrierSmall = new Barrier(2);
      var smallNames = new ConcurrentBag<string>();
      pool.RunBatches(20, batchSize: 10, CancellationToken.None, (start, end) => {
        smallNames.Add(Thread.CurrentThread.Name ?? "");
        barrierSmall.SignalAndWait(TimeSpan.FromSeconds(5));
        return true;
      });
      Assert.AreEqual(2, smallNames.Count);
      Assert.IsTrue(smallNames.All(n => n.StartsWith("VsChromium Indexing Core #", StringComparison.Ordinal)));

      // Full-width job when uncontended (6 batches, barrier across all 6 workers): uses 2 Core + 4 Elastic.
      using var barrierFull = new Barrier(6);
      var fullNames = new ConcurrentBag<string>();
      pool.RunBatches(60, batchSize: 10, CancellationToken.None, (start, end) => {
        fullNames.Add(Thread.CurrentThread.Name ?? "");
        barrierFull.SignalAndWait(TimeSpan.FromSeconds(5));
        return true;
      });
      Assert.AreEqual(2, fullNames.Count(n => n.StartsWith("VsChromium Indexing Core #", StringComparison.Ordinal)));
      Assert.AreEqual(4, fullNames.Count(n => n.StartsWith("VsChromium Indexing Elastic #", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ContendedModeRestrictsExecutionToCoreWorkersWithoutStalling() {
      using var pool = new IndexingWorkerPool(
        coreThreadCount: 2,
        elasticThreadCount: 6,
        contentionMode: IndexingWorkerPool.ContentionMode.AlwaysContended);

      var visited = new int[200];
      var names = new ConcurrentBag<string>();
      pool.RunBatches(visited.Length, batchSize: 5, CancellationToken.None, (start, end) => {
        names.Add(Thread.CurrentThread.Name ?? "");
        for (var i = start; i < end; i++) {
          Interlocked.Increment(ref visited[i]);
        }
        return true;
      });

      Assert.IsTrue(visited.All(v => v == 1));
      Assert.IsTrue(names.All(n => n.StartsWith("VsChromium Indexing Core #", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RunBatchesCoversEntireRangeAndRespectsCancellationAndExceptions() {
      using var pool = new IndexingWorkerPool(coreThreadCount: 2, elasticThreadCount: 2);
      var visited = new int[373];
      pool.RunBatches(visited.Length, batchSize: 7, CancellationToken.None, (start, end) => {
        for (var i = start; i < end; i++) {
          Interlocked.Increment(ref visited[i]);
        }
        return true;
      });
      Assert.IsTrue(visited.All(v => v == 1));

      using (var cts = new CancellationTokenSource()) {
        Assert.Throws<OperationCanceledException>(() => {
          pool.RunBatches(1000, batchSize: 4, cts.Token, (start, end) => {
            cts.Cancel();
            return true;
          });
        });
      }

      Assert.Throws<InvalidOperationException>(() => {
        pool.RunBatches(100, batchSize: 5, CancellationToken.None, (start, end) => {
          if (start >= 20) {
            throw new InvalidOperationException("boom");
          }
          return true;
        });
      });
    }

    [TestMethod]
    public async Task SchedulerRunsRecursiveAsyncTasksOnPoolThreadsWithoutDeadlock() {
      using var pool = new IndexingWorkerPool(coreThreadCount: 2, elasticThreadCount: 2);
      var threadNames = new ConcurrentBag<string>();

      Task<int> TraverseAsync(int depth) {
        return Task.Factory.StartNew(async () => {
          threadNames.Add(Thread.CurrentThread.Name ?? "");
          if (depth <= 0) {
            return 1;
          }
          var children = new[] { TraverseAsync(depth - 1), TraverseAsync(depth - 1) };
          var results = await Task.WhenAll(children);
          threadNames.Add(Thread.CurrentThread.Name ?? "");
          return results[0] + results[1];
        }, CancellationToken.None, TaskCreationOptions.DenyChildAttach, pool.Scheduler).Unwrap();
      }

      var sum = await TraverseAsync(5);
      Assert.AreEqual(32, sum);
      Assert.IsTrue(threadNames.All(n => n.StartsWith("VsChromium Indexing ", StringComparison.Ordinal)));
    }
  }
}
