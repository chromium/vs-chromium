// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.Ipc.TypedEvents;

namespace VsChromiumServer.ProgressTracking {
  public abstract class ProgressTrackerBase : IProgressTracker {
    private readonly object _lock = new object();
    private readonly TimeSpan _refreshDelay = TimeSpan.FromMilliseconds(20);
    private readonly ITypedEventSender _typedEventSender;
    private int _currentStep;
    private bool _eventsSent;
    private DateTime _lastUpdatedUtc;

    protected ProgressTrackerBase(ITypedEventSender typedEventSender) {
      _typedEventSender = typedEventSender;
      _lastUpdatedUtc = DateTime.UtcNow;
    }

    public abstract int TotalStepCount { get; }

    public void Step(ProgressTrackerDisplayTextProvider progressTrackerDisplayTextProvider) {
      Interlocked.Increment(ref _currentStep);

      if (IsTimeToSendEvent()) {
        _eventsSent = true;
        SendProgressEvent(progressTrackerDisplayTextProvider);
      }
    }

    public void Dispose() {
      // Notify of end only if we notified at least once
      if (_eventsSent) {
        _currentStep = TotalStepCount;
        SendProgressEvent((x, y) => "Done!");
      }
    }

    private bool IsTimeToSendEvent() {
      var now = DateTime.UtcNow;
      var timespan = now - _lastUpdatedUtc;
      if (timespan < _refreshDelay)
        return false;

      lock (_lock) {
        timespan = now - _lastUpdatedUtc;
        _lastUpdatedUtc = now;
        return (timespan >= _refreshDelay);
      }
    }

    private void SendProgressEvent(ProgressTrackerDisplayTextProvider progressTrackerDisplayTextProvider) {
      _typedEventSender.SendEventAsync(new ProgressReportEvent {
        DisplayText = progressTrackerDisplayTextProvider(_currentStep, TotalStepCount),
        Completed = _currentStep,
        Total = TotalStepCount,
      });
    }
  }
}
