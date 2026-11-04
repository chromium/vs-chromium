// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// Records the duration of the main server operations (file system scans, file database updates,
  /// requests), reported by "server/statistics". Thread safe.
  /// </summary>
  public static class OperationTimings {
    public const string FileSystemScan = "FileSystemScan";
    public const string FileDatabaseUpdate = "FileDatabaseUpdate";
    /// <summary>Building the trigram filters in the background after a database update.</summary>
    public const string TrigramFiltersBuild = "TrigramFiltersBuild";
    public const string RequestPrefix = "Request:";

    /// <summary>The number of recent durations kept per operation (see <see cref="OperationTiming.RecentMs"/>).</summary>
    public const int RecentCount = 20;

    private static readonly object Lock = new object();
    private static readonly Dictionary<string, Entry> Timings = new Dictionary<string, Entry>();

    public static void Record(string name, TimeSpan duration) {
      Record(name, duration, DateTime.UtcNow);
    }

    /// <summary>
    /// Records an operation that ended at <paramref name="endUtc"/>. No allocation, except the first
    /// time an operation is recorded.
    /// </summary>
    public static void Record(string name, TimeSpan duration, DateTime endUtc) {
      var ms = duration.TotalMilliseconds;
      var endUnixMs = (endUtc.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond;
      lock (Lock) {
        if (!Timings.TryGetValue(name, out var timing)) {
          timing = new Entry(name);
          Timings.Add(name, timing);
        }
        timing.Count++;
        timing.LastMs = ms;
        timing.TotalMs += ms;
        timing.MaxMs = Math.Max(timing.MaxMs, ms);
        timing.RecentMs[timing.RecentNext] = ms;
        timing.RecentEndUnixMs[timing.RecentNext] = endUnixMs;
        timing.RecentNext = (timing.RecentNext + 1) % RecentCount;
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
          .Select(x => x.ToTiming())
          .ToList();
      }
    }

    /// <summary>The timings of an operation (guarded by <see cref="Lock"/>).</summary>
    private sealed class Entry {
      public readonly string Name;
      public long Count;
      public double LastMs;
      public double TotalMs;
      public double MaxMs;
      /// <summary>The last <see cref="RecentCount"/> durations and end times (ring buffers).</summary>
      public readonly double[] RecentMs = new double[RecentCount];
      public readonly long[] RecentEndUnixMs = new long[RecentCount];
      /// <summary>The next slot of the ring buffers.</summary>
      public int RecentNext;

      public Entry(string name) {
        Name = name;
      }

      public OperationTiming ToTiming() {
        var count = (int)Math.Min(Count, RecentCount);
        var recentMs = new double[count];
        var recentEnd = new long[count];
        // Oldest first: the slot after the last one, if the ring buffers are full.
        var first = Count >= RecentCount ? RecentNext : 0;
        for (var i = 0; i < count; i++) {
          recentMs[i] = RecentMs[(first + i) % RecentCount];
          recentEnd[i] = RecentEndUnixMs[(first + i) % RecentCount];
        }
        return new OperationTiming {
          Name = Name, Count = Count, LastMs = LastMs, TotalMs = TotalMs, MaxMs = MaxMs, RecentMs = recentMs,
          RecentEndUnixMs = recentEnd,
        };
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
