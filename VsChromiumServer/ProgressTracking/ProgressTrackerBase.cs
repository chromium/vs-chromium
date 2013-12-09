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
      this._typedEventSender = typedEventSender;
      this._lastUpdatedUtc = DateTime.UtcNow;
    }

    public abstract int TotalStepCount { get; }

    public void Step(ProgressTrackerDisplayTextProvider progressTrackerDisplayTextProvider) {
      Interlocked.Increment(ref this._currentStep);

      if (IsTimeToSendEvent()) {
        this._eventsSent = true;
        SendProgressEvent(progressTrackerDisplayTextProvider);
      }
    }

    public void Dispose() {
      // Notify of end only if we notified at least once
      if (this._eventsSent) {
        this._currentStep = TotalStepCount;
        SendProgressEvent((x, y) => "Done!");
      }
    }

    private bool IsTimeToSendEvent() {
      var now = DateTime.UtcNow;
      var timespan = now - this._lastUpdatedUtc;
      if (timespan < this._refreshDelay)
        return false;

      lock (this._lock) {
        timespan = now - this._lastUpdatedUtc;
        this._lastUpdatedUtc = now;
        return (timespan >= this._refreshDelay);
      }
    }

    private void SendProgressEvent(ProgressTrackerDisplayTextProvider progressTrackerDisplayTextProvider) {
      this._typedEventSender.SendEventAsync(new ProgressReportEvent {
        DisplayText = progressTrackerDisplayTextProvider(this._currentStep, TotalStepCount),
        Completed = this._currentStep,
        Total = TotalStepCount,
      });
    }
  }
}
