// Copyright 2017 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;

namespace VsChromium.Core.Threads {
  /// <summary>
  /// A timer that fires a <see cref="Elapsed"/> event periodically.
  /// The timer can be started, stopped, all of which reset the timer to the
  /// beginning of the period.
  /// </summary>
  public class PeriodicTimer {
    private readonly TimeSpan _period;
    private readonly Timer _timer;
    private readonly object _lock = new object();
    private volatile bool _running;

    public PeriodicTimer(TimeSpan period) {
      _period = period;
      _timer = new Timer(Callback, null, -1, -1);
    }

    public event EventHandler Elapsed;

    public void Start() {
      lock (_lock) {
        _running = true;
        _timer.Change(-1L, (long) _period.TotalMilliseconds);
      }
    }

    public void Stop() {
      lock (_lock) {
        if (_running) {
          _running = false;
          _timer.Change(-1, -1);
        }
      }
    }

    public void Dispose() {
      _timer.Dispose();
    }

    private void Callback(object state) {
      if (_running) {
        OnElapsed();
      }
    }

    protected virtual void OnElapsed() {
      Elapsed?.Invoke(this, EventArgs.Empty);
    }
  }
}