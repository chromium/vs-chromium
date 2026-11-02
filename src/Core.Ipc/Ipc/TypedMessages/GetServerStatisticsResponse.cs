// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// Resource usage of the server process. Durations are in milliseconds, sizes in bytes. Values
  /// that are not available on the current runtime/platform are -1.
  /// </summary>
  public class GetServerStatisticsResponse : TypedResponse {
    public GetServerStatisticsResponse() {
      OperationTimings = new List<OperationTiming>();
    }

    // Process

    public string RuntimeDescription { get; set; }
    public int ProcessId { get; set; }
    public int ProcessorCount { get; set; }
    public double UptimeMs { get; set; }
    public double CpuTotalMs { get; set; }
    public double CpuUserMs { get; set; }
    public double CpuKernelMs { get; set; }
    public long WorkingSetBytes { get; set; }
    public long PeakWorkingSetBytes { get; set; }
    public long PrivateBytes { get; set; }
    public int ThreadCount { get; set; }
    public int HandleCount { get; set; }

    // Managed memory

    /// <summary>Bytes currently allocated on the GC heap.</summary>
    public long GcHeapBytes { get; set; }
    /// <summary>Total bytes allocated since the process started (-1 if not available).</summary>
    public long GcTotalAllocatedBytes { get; set; }
    public int GcGen0Collections { get; set; }
    public int GcGen1Collections { get; set; }
    public int GcGen2Collections { get; set; }
    /// <summary>Total time spent in GC pauses (-1 if not available).</summary>
    public double GcTotalPauseMs { get; set; }

    // Native memory and index

    /// <summary>Native memory explicitly allocated by the server (e.g. file contents).</summary>
    public long NativeAllocatedBytes { get; set; }
    public long IndexedFileContentsBytes { get; set; }
    public long FileCount { get; set; }
    public long SearchableFileCount { get; set; }
    public int ProjectCount { get; set; }

    // Timings

    /// <summary>Timings of the main server operations, measured inside the server.</summary>
    public List<OperationTiming> OperationTimings { get; set; }
  }

  public class OperationTiming {
    /// <summary>
    /// Name of the operation, e.g. "FileSystemScan", "FileDatabaseUpdate", "Request:SearchCodeRequest".
    /// </summary>
    public string Name { get; set; }
    public long Count { get; set; }
    public double LastMs { get; set; }
    public double TotalMs { get; set; }
    public double MaxMs { get; set; }
  }
}
