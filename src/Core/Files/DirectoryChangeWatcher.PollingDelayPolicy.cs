// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class PollingDelayPolicy {
      private readonly IDateTimeProvider _dateTimeProvider;
      private readonly TimeSpan _checkpointDelay;
      private readonly TimeSpan _maxDelay;
      private DateTime _lastPollUtc;
      private DateTime _lastCheckpointUtc;

      private static class ClassLogger {
        static ClassLogger() {
#if DEBUG
          //LogInfoEnabled = true;
#endif
        }
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public static bool LogInfoEnabled { get; set; }
      }

      public PollingDelayPolicy(IDateTimeProvider dateTimeProvider, TimeSpan checkpointDelay, TimeSpan maxDelay) {
        _dateTimeProvider = dateTimeProvider;
        _checkpointDelay = checkpointDelay;
        _maxDelay = maxDelay;
        Restart();
      }

      /// <summary>
      /// Called when all events have been flushed, resets all timers.
      /// </summary>
      public void Restart() {
        _lastPollUtc = _lastCheckpointUtc = _dateTimeProvider.UtcNow;
      }

      /// <summary>
      /// Called when a new event instance occurred, resets the "checkpoint"
      /// timer.
      /// </summary>
      public void Checkpoint() {
        _lastCheckpointUtc = _dateTimeProvider.UtcNow;
      }

      /// <summary>
      /// Returns <code>true</code> when either the maxmium or checkpoint delay
      /// has expired.
      /// </summary>
      public bool WaitTimeExpired() {
        var now = _dateTimeProvider.UtcNow;

        var result = (now - _lastPollUtc >= _maxDelay) ||
                     (now - _lastCheckpointUtc >= _checkpointDelay);
        if (result) {
          if (ClassLogger.LogInfoEnabled) {
            Logger.LogInfo("Timer expired: now={0}, checkpoint={1} msec, start={2} msec, checkpointDelay={3:n0} msec, maxDelay={4:n0} msec",
              now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
              (now - _lastCheckpointUtc).TotalMilliseconds,
              (now - _lastPollUtc).TotalMilliseconds,
              _checkpointDelay.TotalMilliseconds,
              _maxDelay.TotalMilliseconds);
          }
        }
        return result;
      }
    }
  }
}