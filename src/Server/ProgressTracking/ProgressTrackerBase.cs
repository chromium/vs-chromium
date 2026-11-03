// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Threading;

namespace VsChromium.Server.ProgressTracking {
  public abstract class ProgressTrackerBase : IProgressTracker {
    private readonly object _lock = new object();
    private readonly long  _millisecondsBetweenRefresh = 100;
    private readonly IProgressEventSender _progressEventSender;
    private readonly ITickCountProvider _tickCountProvider = new EnvironmentTickCountProvider();
    private int _currentStep;
    private bool _eventsSent;
    private long _lastUpdatedTickCount;

    protected ProgressTrackerBase(IProgressEventSender progressEventSender) {
      _progressEventSender = progressEventSender;
      _lastUpdatedTickCount = GetTickCount();
    }

    public abstract int TotalStepCount { get; }

    private long GetTickCount() {
      return _tickCountProvider.TickCount;
    }

    public bool Step() {
      Interlocked.Increment(ref _currentStep);
      return IsTimeToSendEvent();
    }

    public void DisplayProgress(DisplayProgressCallback displayProgressCallback) {
      _eventsSent = true;
      SendProgressEvent(displayProgressCallback);
    }

    public void Dispose() {
      // Notify of end only if we notified at least once
      if (_eventsSent) {
        _currentStep = TotalStepCount;
        SendProgressEvent((x, y) => "Done!");
      }
    }

    private bool IsTimeToSendEvent() {
      var now = GetTickCount();
      var timespan = now - _lastUpdatedTickCount;
      if (timespan < _millisecondsBetweenRefresh)
        return false;

      // Ensures only one thread returns "true" if we hit this from multiple
      // threads.
      lock (_lock) {
        timespan = now - _lastUpdatedTickCount;
        _lastUpdatedTickCount = now;
        return (timespan >= _millisecondsBetweenRefresh);
      }
    }

    private void SendProgressEvent(DisplayProgressCallback displayProgressCallback) {
      _progressEventSender.SendProgress(displayProgressCallback(_currentStep, TotalStepCount), _currentStep, TotalStepCount);
    }
  }
}
