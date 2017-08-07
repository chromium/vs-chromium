// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.ProgressTracking {
  public class PartialProgressReporter {
    private readonly long _delayMsec;
    private readonly Action _action;
    private readonly ITickCountProvider _tickCountProvider = new EnvironmentTickCountProvider();
    private readonly object _lock = new object();
    private long _lastReportTick;

    public PartialProgressReporter(TimeSpan delay, Action action) {
      _delayMsec = (long) delay.TotalMilliseconds;
      _action = action;
      _lastReportTick = _tickCountProvider.TickCount;
    }

    public void ReportProgressNow() {
      ReportProgressWorkder(true);
    }

    public void ReportProgress() {
      ReportProgressWorkder(false);
    }

    private void ReportProgressWorkder(bool force) {
      var now = _tickCountProvider.TickCount;
      if (!force) {
        if (now - _lastReportTick < _delayMsec)
          return;
      }

      bool isRunner;
      lock (_lock) {
        var last = _lastReportTick;
        _lastReportTick = now;
        isRunner = (now - last >= _delayMsec);
      }

      if (!isRunner)
        return;
      _action();
    }
  }
}