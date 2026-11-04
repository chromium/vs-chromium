// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Cache {
  /// <summary>
  /// When to save the index cache: the index is "dirty" after each update (full load, tree
  /// changes, edits), and saved once the changes settle (<see cref="SettleDelay"/> without update),
  /// but at most <see cref="MaxDelay"/> after the first unsaved change (continuous changes, e.g.
  /// a build, still save regularly), or immediately when requested (e.g. the first load of a
  /// project without cache).
  /// <para>
  /// A save runs on a background thread (below normal priority) and saves the state of the last
  /// update when it starts (a consistent snapshot): updates during a save make the index dirty
  /// again (saved later, same policy); the save is not cancelled (it would never complete during
  /// continuous changes). <see cref="CancelSave"/> cancels a running save (e.g. a new full load):
  /// the index stays dirty.
  /// </para>
  /// <para>
  /// No save at shutdown: <see cref="Dispose"/> cancels a running save and doesn't wait for it
  /// (the previous saved index stays valid: written to a temporary file, then renamed).
  /// </para>
  /// </summary>
  public sealed class IndexCacheSaveScheduler : IDisposable {
    private readonly object _lock = new object();
    private readonly Action<CancellationToken> _save;
    private readonly Func<DateTime> _utcNow;
    private readonly Timer _timer;
    private bool _dirty;
    private bool _saveRequested;
    private DateTime _firstUnsavedUtc;
    private DateTime _lastUpdateUtc;
    private Task _saveTask = Task.CompletedTask;
    private CancellationTokenSource _saveCancellation;
    private bool _disposed;

    public IndexCacheSaveScheduler(TimeSpan settleDelay, TimeSpan maxDelay, Action<CancellationToken> save,
                                   Func<DateTime> utcNow = null) {
      SettleDelay = settleDelay;
      MaxDelay = maxDelay;
      _save = save;
      _utcNow = utcNow ?? (() => DateTime.UtcNow);
      _timer = new Timer(_ => OnTimer());
    }

    public TimeSpan SettleDelay { get; }
    public TimeSpan MaxDelay { get; }

    /// <summary>The index changed since the last save (started).</summary>
    public bool IsDirty {
      get { lock (_lock) { return _dirty; } }
    }

    /// <summary>A save is running.</summary>
    public bool IsSaving {
      get { lock (_lock) { return !_saveTask.IsCompleted; } }
    }

    /// <summary>The index changed: saves it later (see the policy), or as soon as possible if <paramref name="immediate"/>.</summary>
    public void NotifyChanged(bool immediate) {
      lock (_lock) {
        if (_disposed)
          return;
        var now = _utcNow();
        if (!_dirty) {
          _dirty = true;
          _firstUnsavedUtc = now;
        }
        _lastUpdateUtc = now;
        _saveRequested |= immediate;
        ScheduleLocked();
      }
    }

    /// <summary>
    /// The time of the next save of a dirty index: <see cref="SettleDelay"/> after the last update,
    /// but at most <see cref="MaxDelay"/> after the first unsaved update.
    /// </summary>
    public static DateTime GetSaveTime(DateTime firstUnsavedUtc, DateTime lastUpdateUtc, TimeSpan settleDelay, TimeSpan maxDelay) {
      var settled = lastUpdateUtc + settleDelay;
      var latest = firstUnsavedUtc + maxDelay;
      return settled < latest ? settled : latest;
    }

    private void ScheduleLocked() {
      // A running save reschedules when it completes.
      if (_disposed || !_dirty || !_saveTask.IsCompleted)
        return;
      var due = _saveRequested ? TimeSpan.Zero : GetSaveTime(_firstUnsavedUtc, _lastUpdateUtc, SettleDelay, MaxDelay) - _utcNow();
      _timer.Change(due < TimeSpan.Zero ? TimeSpan.Zero : due, Timeout.InfiniteTimeSpan);
    }

    private void OnTimer() {
      lock (_lock) {
        if (_disposed || !_dirty || !_saveTask.IsCompleted)
          return;
        // Not due yet (e.g. updates since the timer was set): reschedule.
        if (!_saveRequested && GetSaveTime(_firstUnsavedUtc, _lastUpdateUtc, SettleDelay, MaxDelay) > _utcNow()) {
          ScheduleLocked();
          return;
        }
        _dirty = false;
        _saveRequested = false;
        var cancellation = new CancellationTokenSource();
        _saveCancellation = cancellation;
        _saveTask = Task.Factory.StartNew(() => RunSave(cancellation), CancellationToken.None,
          TaskCreationOptions.LongRunning, TaskScheduler.Default);
        // Note: After the task completes (IsCompleted), so that the next save can start.
        _saveTask.ContinueWith(_ => {
          lock (_lock) {
            ScheduleLocked();
          }
        }, TaskScheduler.Default);
      }
    }

    private void RunSave(CancellationTokenSource cancellation) {
      Thread.CurrentThread.Priority = ThreadPriority.BelowNormal;
      try {
        _save(cancellation.Token);
      }
      catch (OperationCanceledException) {
        lock (_lock) {
          // Not saved: still dirty (saved later, after the next update).
          _dirty = true;
        }
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error saving the index cache.");
      }
      finally {
        Thread.CurrentThread.Priority = ThreadPriority.Normal;
        lock (_lock) {
          if (ReferenceEquals(_saveCancellation, cancellation)) {
            _saveCancellation = null;
          }
        }
      }
    }

    /// <summary>Cancels a running save (the index stays dirty).</summary>
    public void CancelSave() {
      lock (_lock) {
        _saveCancellation?.Cancel();
      }
    }

    /// <summary>Waits for the running save (tests, invalidation). Returns false on timeout.</summary>
    public bool WaitForSave(TimeSpan timeout) {
      Task task;
      lock (_lock) {
        task = _saveTask;
      }
      try {
        return task.Wait(timeout);
      }
      catch (AggregateException) {
        return true;
      }
    }

    /// <summary>
    /// Stops: no more saves, a running save is cancelled and not waited for (no save at shutdown).
    /// </summary>
    public void Dispose() {
      lock (_lock) {
        _disposed = true;
        _saveCancellation?.Cancel();
        _timer.Dispose();
      }
    }
  }
}
