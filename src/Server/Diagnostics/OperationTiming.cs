// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// The timings of an operation recorded by <see cref="OperationTimings"/>.
  /// </summary>
  public class OperationTiming {
    /// <summary>
    /// Name of the operation, e.g. "FileSystemScan", "FileDatabaseUpdate", "Request:search/code".
    /// </summary>
    public string Name { get; set; }
    public long Count { get; set; }
    public double LastMs { get; set; }
    public double TotalMs { get; set; }
    public double MaxMs { get; set; }
    /// <summary>The last durations (at most <see cref="OperationTimings.RecentCount"/>), oldest first.</summary>
    public double[] RecentMs { get; set; }
    /// <summary>The end times of <see cref="RecentMs"/>, in milliseconds since the Unix epoch (UTC).</summary>
    public long[] RecentEndUnixMs { get; set; }
  }
}
