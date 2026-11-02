// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;

namespace VsChromium.Tests.EndToEnd.Benchmarks;

/// <summary>A sample of the resource usage of a process, measured from outside the process.</summary>
public readonly record struct ProcessSample(TimeSpan Time, double CpuMs, long WorkingSetBytes, long PrivateBytes);

/// <summary>Summary of the samples collected over a period of time.</summary>
public sealed record ProcessUsage(double WallMs, double CpuMs, long PeakWorkingSetBytes, long PeakPrivateBytes,
                                  long EndWorkingSetBytes, long EndPrivateBytes) {
  /// <summary>Average number of cores used over the period.</summary>
  public double AverageCores => WallMs > 0 ? CpuMs / WallMs : 0;
}

/// <summary>
/// Samples the CPU time and memory (working set, private bytes: they include native memory) of a
/// process every <see cref="Interval"/>, from a background thread of the benchmark process.
/// </summary>
public sealed class ProcessSampler : IDisposable {
  public static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(50);

  private readonly Process _process;
  private readonly Stopwatch _clock = Stopwatch.StartNew();
  private readonly List<ProcessSample> _samples = new();
  private readonly object _lock = new();
  private readonly Thread _thread;
  private volatile bool _stop;

  public ProcessSampler(Process process) {
    _process = process;
    TakeSample();
    _thread = new Thread(Run) { IsBackground = true, Name = "ProcessSampler" };
    _thread.Start();
  }

  /// <summary>Takes a sample now and returns it.</summary>
  public ProcessSample TakeSample() {
    _process.Refresh();
    var sample = new ProcessSample(_clock.Elapsed, _process.TotalProcessorTime.TotalMilliseconds,
                                   _process.WorkingSet64, _process.PrivateMemorySize64);
    lock (_lock) { _samples.Add(sample); }
    return sample;
  }

  /// <summary>Returns the usage between two samples (inclusive), including the peaks in between.</summary>
  public ProcessUsage GetUsage(ProcessSample start, ProcessSample end) {
    List<ProcessSample> range;
    lock (_lock) { range = _samples.Where(s => s.Time >= start.Time && s.Time <= end.Time).ToList(); }
    return new ProcessUsage(
      WallMs: (end.Time - start.Time).TotalMilliseconds,
      CpuMs: end.CpuMs - start.CpuMs,
      PeakWorkingSetBytes: range.Max(s => s.WorkingSetBytes),
      PeakPrivateBytes: range.Max(s => s.PrivateBytes),
      EndWorkingSetBytes: end.WorkingSetBytes,
      EndPrivateBytes: end.PrivateBytes);
  }

  private void Run() {
    while (!_stop) {
      Thread.Sleep(Interval);
      try {
        if (_process.HasExited) {
          return;
        }
        TakeSample();
      } catch (InvalidOperationException) {
        return; // Process exited.
      }
    }
  }

  public void Dispose() {
    _stop = true;
    _thread.Join();
  }
}
