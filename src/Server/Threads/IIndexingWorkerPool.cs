// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using System.Threading.Tasks;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Elastic worker pool dedicated to bulk background indexing work (parallel directory traversal,
  /// bulk file loading, and background trigram filter construction). Keeps a small number of core
  /// <see cref="ThreadPriority.Normal"/> workers active at all times for guaranteed progress, and
  /// scales up with elastic <see cref="ThreadPriority.Normal"/> workers only while a dedicated
  /// <see cref="ThreadPriority.BelowNormal"/> canary thread observes spare CPU capacity.
  /// Interactive search queries do not use this pool.
  /// </summary>
  public interface IIndexingWorkerPool : IDisposable {
    int CoreThreadCount { get; }
    int ElasticThreadCount { get; }
    int TotalThreadCount { get; }

    /// <summary>
    /// <see cref="TaskScheduler"/> backed by this pool's worker threads (for async tree traversal).
    /// </summary>
    TaskScheduler Scheduler { get; }

    /// <summary>
    /// Processes the range [0, <paramref name="count"/>) in parallel across up to
    /// <see cref="TotalThreadCount"/> pool workers by batches of <paramref name="batchSize"/>.
    /// </summary>
    void RunBatches(
      int count,
      int batchSize,
      CancellationToken cancellationToken,
      Func<int, int, bool> processBatch);

    /// <summary>
    /// Processes the range [0, <paramref name="count"/>) in parallel across up to
    /// <paramref name="maxDegreeOfParallelism"/> pool workers by batches of <paramref name="batchSize"/>.
    /// </summary>
    void RunBatches(
      int count,
      int batchSize,
      int maxDegreeOfParallelism,
      CancellationToken cancellationToken,
      Func<int, int, bool> processBatch);
  }
}
