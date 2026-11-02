// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// Records the duration of the main server operations (file system scans, file database updates,
  /// requests), reported by <see cref="GetServerStatisticsRequest"/>. Thread safe.
  /// </summary>
  public static class OperationTimings {
    public const string FileSystemScan = "FileSystemScan";
    public const string FileDatabaseUpdate = "FileDatabaseUpdate";
    public const string RequestPrefix = "Request:";

    private static readonly object Lock = new object();
    private static readonly Dictionary<string, OperationTiming> Timings = new Dictionary<string, OperationTiming>();

    public static void Record(string name, TimeSpan duration) {
      var ms = duration.TotalMilliseconds;
      lock (Lock) {
        OperationTiming timing;
        if (!Timings.TryGetValue(name, out timing)) {
          timing = new OperationTiming { Name = name };
          Timings.Add(name, timing);
        }
        timing.Count++;
        timing.LastMs = ms;
        timing.TotalMs += ms;
        timing.MaxMs = Math.Max(timing.MaxMs, ms);
      }
    }

    /// <summary>
    /// Measures the duration of a block of code: <code>using (OperationTimings.Measure(name)) { ... }</code>.
    /// </summary>
    public static Scope Measure(string name) {
      return new Scope(name);
    }

    public static List<OperationTiming> GetSnapshot() {
      lock (Lock) {
        return Timings.Values
          .OrderBy(x => x.Name, StringComparer.Ordinal)
          .Select(x => new OperationTiming { Name = x.Name, Count = x.Count, LastMs = x.LastMs, TotalMs = x.TotalMs, MaxMs = x.MaxMs })
          .ToList();
      }
    }

    public struct Scope : IDisposable {
      private readonly string _name;
      private readonly Stopwatch _stopwatch;

      public Scope(string name) {
        _name = name;
        _stopwatch = Stopwatch.StartNew();
      }

      public void Dispose() {
        if (_stopwatch != null) {
          Record(_name, _stopwatch.Elapsed);
        }
      }
    }
  }
}
