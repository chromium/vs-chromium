// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Threading;

namespace VsChromium.Core.Threads {
  /// <summary>
  /// Processes the range [0, count) in parallel, by batches of consecutive indices handed out to
  /// workers with an atomic cursor (dynamic load balancing: a worker that is descheduled or gets
  /// expensive items doesn't delay the others). All the workers start immediately (one per
  /// processor), which matters for short operations (a few milliseconds).
  /// </summary>
  public static class ParallelBatches {
    /// <summary>
    /// Calls <paramref name="processBatch"/> with (start, end) for batches of at most
    /// <paramref name="batchSize"/> indices until all indices are processed, or until
    /// <paramref name="processBatch"/> returns <code>false</code> (no more batches are started).
    /// <paramref name="processBatch"/> is called concurrently from several threads. Throws <see
    /// cref="OperationCanceledException"/> if <paramref name="cancellationToken"/> is canceled.
    /// </summary>
    public static void For(int count, int batchSize, CancellationToken cancellationToken, Func<int, int, bool> processBatch) {
      if (count <= 0)
        return;
      batchSize = Math.Max(1, batchSize);
      var batchCount = (count + batchSize - 1) / batchSize;
      var workerCount = Math.Max(1, Math.Min(Environment.ProcessorCount, batchCount));

      var cursor = 0;
      var stopped = 0;
      // Batches are handed out in an order that spreads the workers over the whole range (ticket
      // "t" maps to batch #(t / workerCount) of region #(t % workerCount)), as a static range
      // partitioning would, so that operations stopping early (e.g. after a maximum number of
      // results) see a sample of the whole range, not only its beginning.
      var batchesPerRegion = (batchCount + workerCount - 1) / workerCount;
      var ticketCount = batchesPerRegion * workerCount;
      void Worker() {
        while (Volatile.Read(ref stopped) == 0) {
          cancellationToken.ThrowIfCancellationRequested();
          var ticket = Interlocked.Increment(ref cursor) - 1;
          if (ticket >= ticketCount)
            break;
          var batch = (ticket % workerCount) * batchesPerRegion + ticket / workerCount;
          if (batch >= batchCount)
            continue;
          var start = batch * batchSize;
          if (!processBatch(start, Math.Min(count, start + batchSize)))
            Volatile.Write(ref stopped, 1);
        }
      }

      if (workerCount == 1) {
        Worker();
        return;
      }

      ParallelEnumerable.Range(0, workerCount)
        .WithDegreeOfParallelism(workerCount)
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .WithCancellation(cancellationToken)
        .ForAll(_ => Worker());
    }
  }
}
