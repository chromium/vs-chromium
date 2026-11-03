// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Elastic worker pool for bulk background indexing (parallel directory traversal, bulk file
  /// loading, and background trigram filter construction):
  /// <list type="bullet">
  /// <item>All worker threads run at <see cref="ThreadPriority.Normal"/> so a worker preempted
  /// mid-task never causes priority inversion on <c>Task.WhenAll</c>, locks, or batch completion.</item>
  /// <item><see cref="CoreThreadCount"/> ("core" workers, <c>Clamp(ProcessorCount / 6, 2, 8)</c>,
  /// e.g. 4 on a 24-thread machine) always process queued work to guarantee forward progress under
  /// heavy CPU load while keeping foreground build slowdown small (~10-15% instead of ~50%).</item>
  /// <item><see cref="ElasticThreadCount"/> ("elastic" workers, up to
  /// <c>Max(0, ProcessorCount - 1 - CoreWorkers)</c>) run when a dedicated
  /// <see cref="ThreadPriority.BelowNormal"/> canary thread confirms spare CPU capacity, and step
  /// aside at task/batch boundaries within ~80 ms whenever normal-priority foreground work saturates
  /// all cores. Capping total workers at <c>ProcessorCount - 1</c> on multi-core machines leaves one
  /// core free so the server's own workers never starve the canary when the machine is otherwise idle.</item>
  /// </list>
  /// </summary>
  public sealed class IndexingWorkerPool : IIndexingWorkerPool {
    private const int CanaryPeriodMs = 20;
    private const double CanaryCycleBudgetMs = 60.0;
    private static readonly long CanaryStaleTicks = (long)(Stopwatch.Frequency * 0.080); // 80 ms
    private static readonly long WorkerRecheckTicks = Stopwatch.Frequency / 250; // 4 ms

    public enum ContentionMode {
      Auto,
      AlwaysUncontended,
      AlwaysContended,
    }

    [ThreadStatic]
    private static Worker t_currentWorker;

    private readonly object _lock = new object();
    private readonly Queue<WorkItem> _queue = new Queue<WorkItem>();
    private readonly Stack<Worker> _parkedCore;
    private readonly Stack<Worker> _parkedElastic;
    private readonly List<Worker> _allWorkers;
    private readonly ManualResetEventSlim _canaryWakeSignal = new ManualResetEventSlim(false);
    private readonly ManualResetEventSlim _disposeSignal = new ManualResetEventSlim(false);
    private readonly PoolTaskScheduler _scheduler;
    private readonly ContentionMode _contentionMode;

    private Thread _canaryThread;
    private long _lastCanaryHealthyTick;
    private int _activeSchedulerTasks;
    private int _activeBatchJobs;
    private bool _started;
    private bool _disposed;

    public IndexingWorkerPool()
      : this(
          coreThreadCount: GetDefaultCoreThreadCount(Environment.ProcessorCount),
          elasticThreadCount: GetDefaultElasticThreadCount(Environment.ProcessorCount),
          contentionMode: ContentionMode.Auto) {
    }

    public IndexingWorkerPool(
      int coreThreadCount,
      int elasticThreadCount,
      ContentionMode contentionMode = ContentionMode.Auto) {
      if (coreThreadCount < 0) {
        throw new ArgumentOutOfRangeException(nameof(coreThreadCount));
      }
      if (elasticThreadCount < 0) {
        throw new ArgumentOutOfRangeException(nameof(elasticThreadCount));
      }
      if (coreThreadCount + elasticThreadCount <= 0) {
        throw new ArgumentException("Total worker thread count must be at least 1.");
      }

      CoreThreadCount = coreThreadCount;
      ElasticThreadCount = elasticThreadCount;
      TotalThreadCount = coreThreadCount + elasticThreadCount;
      _contentionMode = contentionMode;
      _parkedCore = new Stack<Worker>(coreThreadCount);
      _parkedElastic = new Stack<Worker>(elasticThreadCount);
      _allWorkers = new List<Worker>(TotalThreadCount);
      _scheduler = new PoolTaskScheduler(this);
    }

    public static int GetDefaultCoreThreadCount(int processorCount) {
      var cores = Math.Max(1, processorCount);
      var maxPoolThreads = cores > 2 ? cores - 1 : cores;
      return Math.Min(maxPoolThreads, Math.Clamp(cores / 6, 2, 8));
    }

    public static int GetDefaultElasticThreadCount(int processorCount) {
      var cores = Math.Max(1, processorCount);
      var maxPoolThreads = cores > 2 ? cores - 1 : cores;
      var coreThreads = GetDefaultCoreThreadCount(cores);
      return Math.Max(0, maxPoolThreads - coreThreads);
    }

    public int CoreThreadCount { get; }
    public int ElasticThreadCount { get; }
    public int TotalThreadCount { get; }
    public TaskScheduler Scheduler => _scheduler;

    public void RunBatches(
      int count,
      int batchSize,
      CancellationToken cancellationToken,
      Func<int, int, bool> processBatch) {
      RunBatches(count, batchSize, TotalThreadCount, cancellationToken, processBatch);
    }

    public void RunBatches(
      int count,
      int batchSize,
      int maxDegreeOfParallelism,
      CancellationToken cancellationToken,
      Func<int, int, bool> processBatch) {
      if (processBatch == null) {
        throw new ArgumentNullException(nameof(processBatch));
      }
      if (maxDegreeOfParallelism <= 0) {
        throw new ArgumentOutOfRangeException(nameof(maxDegreeOfParallelism));
      }

      cancellationToken.ThrowIfCancellationRequested();
      ThrowIfDisposed();
      if (count <= 0) {
        return;
      }

      batchSize = Math.Max(1, batchSize);
      var batchCount = (count + batchSize - 1) / batchSize;
      var maxWorkers = Math.Max(1, Math.Min(TotalThreadCount, maxDegreeOfParallelism));
      var workerCount = Math.Max(1, Math.Min(maxWorkers, batchCount));

      BeginActiveBatchJob();
      try {
        var state = new BatchState(this, count, batchSize, batchCount, workerCount, cancellationToken, processBatch);
        var onPoolThread = t_currentWorker?.Owner == this;
        var enqueuedCount = onPoolThread ? workerCount - 1 : workerCount;
        var item = WorkItem.FromAction(state.ExecuteWorker);
        for (var i = 0; i < enqueuedCount; i++) {
          EnqueueWorkItem(item);
        }

        if (onPoolThread) {
          state.ExecuteWorker();
          while (!state.IsCompleted) {
            if (!TryDequeueAndExecuteOne()) {
              state.WaitBriefly();
            }
          }
        } else {
          state.WaitForCompletion();
        }

        state.ThrowIfFaultedOrCanceled();
        cancellationToken.ThrowIfCancellationRequested();
        ThrowIfDisposed();
      }
      finally {
        EndActiveBatchJob();
      }
    }

    public void Dispose() {
      List<Worker> workersToJoin = null;
      List<Worker> parkedToWake = null;
      Thread canaryToJoin = null;
      lock (_lock) {
        if (_disposed) {
          return;
        }
        Volatile.Write(ref _disposed, true);
        _disposeSignal.Set();
        _canaryWakeSignal.Set();
        if (_started) {
          parkedToWake = new List<Worker>(_parkedCore.Count + _parkedElastic.Count);
          while (_parkedCore.Count > 0) {
            parkedToWake.Add(_parkedCore.Pop());
          }
          while (_parkedElastic.Count > 0) {
            parkedToWake.Add(_parkedElastic.Pop());
          }
          workersToJoin = _allWorkers;
          canaryToJoin = _canaryThread;
        }
      }

      if (parkedToWake != null) {
        foreach (var worker in parkedToWake) {
          worker.Gate.Set();
        }
      }

      if (t_currentWorker?.Owner != this) {
        canaryToJoin?.Join();
        if (workersToJoin != null) {
          foreach (var worker in workersToJoin) {
            worker.Thread.Join();
            worker.Gate.Dispose();
          }
        }
        _canaryWakeSignal.Dispose();
        _disposeSignal.Dispose();
      }
    }

    private void ThrowIfDisposed() {
      if (Volatile.Read(ref _disposed)) {
        throw new OperationCanceledException("IndexingWorkerPool has been disposed.");
      }
    }

    private bool IsCpuContended() {
      switch (_contentionMode) {
        case ContentionMode.AlwaysUncontended:
          return false;
        case ContentionMode.AlwaysContended:
          return true;
        default: {
          var lastTick = Volatile.Read(ref _lastCanaryHealthyTick);
          if (lastTick == 0) {
            return true;
          }
          return Stopwatch.GetTimestamp() - lastTick > CanaryStaleTicks;
        }
      }
    }

    private void BeginActiveBatchJob() {
      if (ElasticThreadCount <= 0 || _contentionMode != ContentionMode.Auto) {
        return;
      }
      lock (_lock) {
        if (_disposed) {
          return;
        }
        EnsureStartedLocked();
        var wasIdle = _activeBatchJobs == 0 && _activeSchedulerTasks == 0;
        _activeBatchJobs++;
        if (wasIdle) {
          _canaryWakeSignal.Set();
        }
      }
    }

    private void EndActiveBatchJob() {
      if (ElasticThreadCount <= 0 || _contentionMode != ContentionMode.Auto) {
        return;
      }
      lock (_lock) {
        if (_activeBatchJobs > 0) {
          _activeBatchJobs--;
        }
      }
    }

    private void EnqueueWorkItem(WorkItem item) {
      Worker toWake = null;
      var fallbackToThreadPool = false;
      var wakeCanary = false;
      lock (_lock) {
        if (_disposed) {
          fallbackToThreadPool = item.Task != null;
        } else {
          EnsureStartedLocked();
          if (item.Task != null && ElasticThreadCount > 0 && _contentionMode == ContentionMode.Auto) {
            var wasIdle = _activeBatchJobs == 0 && _activeSchedulerTasks == 0;
            _activeSchedulerTasks++;
            if (wasIdle) {
              wakeCanary = true;
            }
          }
          _queue.Enqueue(item);
          toWake = TryPopParkedWorkerLocked(allowElastic: !IsCpuContended());
        }
      }

      if (fallbackToThreadPool) {
        // Avoid stranding tasks queued by unawaited operations during container teardown.
        ThreadPool.UnsafeQueueUserWorkItem(s => _scheduler.ExecuteTask((Task)s), item.Task);
        return;
      }
      if (wakeCanary) {
        _canaryWakeSignal.Set();
      }
      toWake?.Gate.Set();
    }

    private void OnSchedulerTaskCompleted() {
      if (ElasticThreadCount <= 0 || _contentionMode != ContentionMode.Auto) {
        return;
      }
      lock (_lock) {
        if (_activeSchedulerTasks > 0) {
          _activeSchedulerTasks--;
        }
      }
    }

    private Worker TryPopParkedWorkerLocked(bool allowElastic) {
      if (_parkedCore.Count > 0) {
        return _parkedCore.Pop();
      }
      if (allowElastic || CoreThreadCount == 0) {
        if (_parkedElastic.Count > 0) {
          return _parkedElastic.Pop();
        }
      }
      return null;
    }

    private void WakeElasticWorkersForPendingWork() {
      List<Worker> toWake = null;
      lock (_lock) {
        if (_disposed || _queue.Count == 0 || _parkedElastic.Count == 0) {
          return;
        }
        var count = Math.Min(_queue.Count, _parkedElastic.Count);
        toWake = new List<Worker>(count);
        for (var i = 0; i < count; i++) {
          toWake.Add(_parkedElastic.Pop());
        }
      }
      if (toWake != null) {
        foreach (var worker in toWake) {
          worker.Gate.Set();
        }
      }
    }

    private void EnsureStartedLocked() {
      if (_started) {
        return;
      }
      _started = true;

      for (var i = 0; i < CoreThreadCount; i++) {
        StartWorkerLocked(isCore: true, index: i);
      }
      for (var i = 0; i < ElasticThreadCount; i++) {
        StartWorkerLocked(isCore: false, index: i);
      }
      if (ElasticThreadCount > 0 && _contentionMode == ContentionMode.Auto) {
        var canary = new Thread(CanaryLoop) {
          IsBackground = true,
          Name = "VsChromium Indexing Canary",
        };
        TrySetPriority(canary, ThreadPriority.BelowNormal);
        _canaryThread = canary;
        canary.Start();
      }
    }

    private void StartWorkerLocked(bool isCore, int index) {
      var worker = new Worker(this, isCore);
      var thread = new Thread(() => WorkerLoop(worker)) {
        IsBackground = true,
        Name = isCore
          ? $"VsChromium Indexing Core #{index}"
          : $"VsChromium Indexing Elastic #{index}",
      };
      TrySetPriority(thread, ThreadPriority.Normal);
      worker.Thread = thread;
      _allWorkers.Add(worker);
      thread.Start();
    }

    private static void TrySetPriority(Thread thread, ThreadPriority priority) {
      try {
        thread.Priority = priority;
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Unable to set indexing thread priority to {0}", priority);
      }
    }

    /// <summary>
    /// Runs at <see cref="ThreadPriority.BelowNormal"/> without ever touching locks or work items.
    /// While the pool has active work, probes scheduling latency every <see cref="CanaryPeriodMs"/>
    /// and stamps <see cref="_lastCanaryHealthyTick"/> whenever spare CPU capacity lets this
    /// <see cref="ThreadPriority.BelowNormal"/> thread wake promptly. Sleeps indefinitely when the
    /// pool is idle.
    /// </summary>
    private void CanaryLoop() {
      while (!Volatile.Read(ref _disposed)) {
        bool active;
        lock (_lock) {
          if (_disposed) {
            return;
          }
          active = _activeBatchJobs > 0 || _activeSchedulerTasks > 0 || _queue.Count > 0;
          if (!active) {
            Volatile.Write(ref _lastCanaryHealthyTick, 0);
            _canaryWakeSignal.Reset();
          }
        }

        if (!active) {
          _canaryWakeSignal.Wait();
          if (Volatile.Read(ref _disposed)) {
            return;
          }
          // Immediate zero-delay probe upon waking from idle so uncontended machines start
          // elastic workers without waiting for the first 20 ms timer period.
          var t0 = Stopwatch.GetTimestamp();
          Thread.Sleep(0);
          if (Stopwatch.GetElapsedTime(t0).TotalMilliseconds < 10.0) {
            Volatile.Write(ref _lastCanaryHealthyTick, Stopwatch.GetTimestamp());
            WakeElasticWorkersForPendingWork();
          }
          continue;
        }

        var cycleStart = Stopwatch.GetTimestamp();
        if (_disposeSignal.Wait(CanaryPeriodMs)) {
          return;
        }
        Thread.Sleep(0);
        if (Stopwatch.GetElapsedTime(cycleStart).TotalMilliseconds < CanaryCycleBudgetMs) {
          var wasContended = IsCpuContended();
          Volatile.Write(ref _lastCanaryHealthyTick, Stopwatch.GetTimestamp());
          if (wasContended) {
            WakeElasticWorkersForPendingWork();
          }
        }
      }
    }

    private bool TryDequeueAndExecuteOne() {
      WorkItem item;
      Worker extraToWake = null;
      lock (_lock) {
        if (_queue.Count == 0) {
          return false;
        }
        item = _queue.Dequeue();
        if (_queue.Count > 0) {
          extraToWake = TryPopParkedWorkerLocked(allowElastic: !IsCpuContended());
        }
      }
      extraToWake?.Gate.Set();
      ExecuteWorkItem(item);
      return true;
    }

    private void ExecuteWorkItem(WorkItem item) {
      if (item.Task != null) {
        try {
          _scheduler.ExecuteTask(item.Task);
        }
        finally {
          OnSchedulerTaskCompleted();
        }
      } else {
        item.Action?.Invoke();
      }
    }

    private void WorkerLoop(Worker self) {
      t_currentWorker = self;
      try {
        while (true) {
          WorkItem item = default;
          var hasItem = false;
          Worker extraToWake = null;
          lock (_lock) {
            if (_disposed) {
              return;
            }
            var canDequeue = self.IsCore || CoreThreadCount == 0 || !IsCpuContended();
            if (canDequeue && _queue.Count > 0) {
              item = _queue.Dequeue();
              hasItem = true;
              if (_queue.Count > 0) {
                extraToWake = TryPopParkedWorkerLocked(allowElastic: !IsCpuContended());
              }
            } else {
              // If an elastic worker is parking while work remains in _queue, ensure any parked
              // core worker is woken to drain _queue.
              if (_queue.Count > 0 && _parkedCore.Count > 0) {
                extraToWake = _parkedCore.Pop();
              }
              self.Gate.Reset();
              if (self.IsCore) {
                _parkedCore.Push(self);
              } else {
                _parkedElastic.Push(self);
              }
            }
          }

          extraToWake?.Gate.Set();
          if (!hasItem) {
            self.Gate.Wait();
            continue;
          }

          ExecuteWorkItem(item);
        }
      }
      finally {
        t_currentWorker = null;
      }
    }

    private Task[] GetScheduledTasksSnapshot() {
      lock (_lock) {
        if (_queue.Count == 0) {
          return Array.Empty<Task>();
        }
        var tasks = new List<Task>(_queue.Count);
        foreach (var item in _queue) {
          if (item.Task != null) {
            tasks.Add(item.Task);
          }
        }
        return tasks.ToArray();
      }
    }

    private sealed class BatchState {
      private readonly IndexingWorkerPool _pool;
      private readonly int _count;
      private readonly int _batchSize;
      private readonly int _batchCount;
      private readonly int _workerCount;
      private readonly int _batchesPerRegion;
      private readonly int _ticketCount;
      private readonly CancellationToken _cancellationToken;
      private readonly Func<int, int, bool> _processBatch;
      private readonly object _latch = new object();

      private int _cursor;
      private int _stopped;
      private int _activeBatches;
      private int _finishedBatches;
      private int _completed;
      private Exception _firstException;
      private OperationCanceledException _canceledException;

      public BatchState(
        IndexingWorkerPool pool,
        int count,
        int batchSize,
        int batchCount,
        int workerCount,
        CancellationToken cancellationToken,
        Func<int, int, bool> processBatch) {
        _pool = pool;
        _count = count;
        _batchSize = batchSize;
        _batchCount = batchCount;
        _workerCount = workerCount;
        _batchesPerRegion = (batchCount + workerCount - 1) / workerCount;
        _ticketCount = _batchesPerRegion * workerCount;
        _cancellationToken = cancellationToken;
        _processBatch = processBatch;
      }

      public bool IsCompleted => Volatile.Read(ref _completed) != 0;

      public void ExecuteWorker() {
        if (IsCompleted) {
          return;
        }

        var isElastic = t_currentWorker is { IsCore: false } && _pool.CoreThreadCount > 0;
        var lastCheckTimestamp = isElastic ? Stopwatch.GetTimestamp() : 0;

        while (Volatile.Read(ref _stopped) == 0 && !IsCompleted) {
          if (isElastic) {
            var now = Stopwatch.GetTimestamp();
            if (now - lastCheckTimestamp >= WorkerRecheckTicks) {
              if (_pool.IsCpuContended()) {
                // Re-queue this worker slot so a core worker picks up remaining batches if needed.
                if (!IsCompleted && Volatile.Read(ref _stopped) == 0) {
                  _pool.EnqueueWorkItem(WorkItem.FromAction(ExecuteWorker));
                }
                return;
              }
              lastCheckTimestamp = now;
            }
          }

          var ticket = Interlocked.Increment(ref _cursor) - 1;
          if (ticket >= _ticketCount) {
            return;
          }
          var batch = (ticket % _workerCount) * _batchesPerRegion + ticket / _workerCount;
          if (batch >= _batchCount) {
            continue;
          }

          var batchExecuted = false;
          Interlocked.Increment(ref _activeBatches);
          try {
            if (Volatile.Read(ref _stopped) != 0) {
              return;
            }
            _cancellationToken.ThrowIfCancellationRequested();
            _pool.ThrowIfDisposed();

            var start = batch * _batchSize;
            batchExecuted = true;
            if (!_processBatch(start, Math.Min(_count, start + _batchSize))) {
              Volatile.Write(ref _stopped, 1);
            }
          }
          catch (OperationCanceledException oce) {
            Volatile.Write(ref _stopped, 1);
            Interlocked.CompareExchange(ref _canceledException, oce, null);
          }
          catch (Exception ex) {
            Volatile.Write(ref _stopped, 1);
            Interlocked.CompareExchange(ref _firstException, ex, null);
          }
          finally {
            if (batchExecuted) {
              Interlocked.Increment(ref _finishedBatches);
            }
            // Note: Read the number of finished batches *after* decrementing the active count:
            // the worker that brings the active count to 0 then sees the batches finished by all
            // the other workers (their increments happen before their decrements). A value read
            // before the decrement lost the completion when two workers finished the last batches
            // at the same time (RunBatches waited forever).
            var active = Interlocked.Decrement(ref _activeBatches);
            if (active == 0 && (Volatile.Read(ref _finishedBatches) >= _batchCount || Volatile.Read(ref _stopped) != 0)) {
              MarkCompleted();
            }
          }
        }
      }

      private void MarkCompleted() {
        if (Interlocked.Exchange(ref _completed, 1) == 0) {
          lock (_latch) {
            Monitor.PulseAll(_latch);
          }
        }
      }

      public void WaitForCompletion() {
        lock (_latch) {
          while (!IsCompleted) {
            Monitor.Wait(_latch);
          }
        }
      }

      public void WaitBriefly() {
        lock (_latch) {
          if (!IsCompleted) {
            Monitor.Wait(_latch, millisecondsTimeout: 1);
          }
        }
      }

      public void ThrowIfFaultedOrCanceled() {
        if (_firstException != null) {
          ExceptionDispatchInfo.Capture(_firstException).Throw();
        }
        if (_canceledException != null) {
          ExceptionDispatchInfo.Capture(_canceledException).Throw();
        }
      }
    }

    private sealed class Worker {
      public readonly IndexingWorkerPool Owner;
      public readonly ManualResetEventSlim Gate = new ManualResetEventSlim(false);
      public readonly bool IsCore;
      public Thread Thread;

      public Worker(IndexingWorkerPool owner, bool isCore) {
        Owner = owner;
        IsCore = isCore;
      }
    }

    private readonly struct WorkItem {
      public readonly Action Action;
      public readonly Task Task;

      private WorkItem(Action action, Task task) {
        Action = action;
        Task = task;
      }

      public static WorkItem FromAction(Action action) => new WorkItem(action, null);
      public static WorkItem FromTask(Task task) => new WorkItem(null, task);
    }

    private sealed class PoolTaskScheduler : TaskScheduler {
      private readonly IndexingWorkerPool _pool;

      public PoolTaskScheduler(IndexingWorkerPool pool) {
        _pool = pool;
      }

      public override int MaximumConcurrencyLevel => _pool.TotalThreadCount;

      internal void ExecuteTask(Task task) {
        TryExecuteTask(task);
      }

      protected override void QueueTask(Task task) {
        _pool.EnqueueWorkItem(WorkItem.FromTask(task));
      }

      protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) {
        return !taskWasPreviouslyQueued && t_currentWorker?.Owner == _pool && TryExecuteTask(task);
      }

      protected override IEnumerable<Task> GetScheduledTasks() {
        return _pool.GetScheduledTasksSnapshot();
      }
    }
  }
}
