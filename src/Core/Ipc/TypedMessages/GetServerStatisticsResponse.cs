// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// Resource usage of the server process. Durations are in milliseconds, sizes in bytes. Values
  /// that are not available on the current runtime/platform are -1.
  /// </summary>
  [ProtoContract]
  public class GetServerStatisticsResponse : TypedResponse {
    public GetServerStatisticsResponse() {
      OperationTimings = new List<OperationTiming>();
    }

    // Process

    [ProtoMember(1)]
    public string RuntimeDescription { get; set; }
    [ProtoMember(2)]
    public int ProcessId { get; set; }
    [ProtoMember(3)]
    public int ProcessorCount { get; set; }
    [ProtoMember(4)]
    public double UptimeMs { get; set; }
    [ProtoMember(5)]
    public double CpuTotalMs { get; set; }
    [ProtoMember(6)]
    public double CpuUserMs { get; set; }
    [ProtoMember(7)]
    public double CpuKernelMs { get; set; }
    [ProtoMember(8)]
    public long WorkingSetBytes { get; set; }
    [ProtoMember(9)]
    public long PeakWorkingSetBytes { get; set; }
    [ProtoMember(10)]
    public long PrivateBytes { get; set; }
    [ProtoMember(11)]
    public int ThreadCount { get; set; }
    [ProtoMember(12)]
    public int HandleCount { get; set; }

    // Managed memory

    /// <summary>Bytes currently allocated on the GC heap.</summary>
    [ProtoMember(20)]
    public long GcHeapBytes { get; set; }
    /// <summary>Total bytes allocated since the process started (-1 if not available).</summary>
    [ProtoMember(21)]
    public long GcTotalAllocatedBytes { get; set; }
    [ProtoMember(22)]
    public int GcGen0Collections { get; set; }
    [ProtoMember(23)]
    public int GcGen1Collections { get; set; }
    [ProtoMember(24)]
    public int GcGen2Collections { get; set; }
    /// <summary>Total time spent in GC pauses (-1 if not available).</summary>
    [ProtoMember(25)]
    public double GcTotalPauseMs { get; set; }

    // Native memory and index

    /// <summary>Native memory explicitly allocated by the server (e.g. file contents).</summary>
    [ProtoMember(30)]
    public long NativeAllocatedBytes { get; set; }
    [ProtoMember(31)]
    public long IndexedFileContentsBytes { get; set; }
    [ProtoMember(32)]
    public long FileCount { get; set; }
    [ProtoMember(33)]
    public long SearchableFileCount { get; set; }
    [ProtoMember(34)]
    public int ProjectCount { get; set; }

    // Timings

    /// <summary>Timings of the main server operations, measured inside the server.</summary>
    [ProtoMember(40)]
    public List<OperationTiming> OperationTimings { get; set; }
  }

  [ProtoContract]
  public class OperationTiming {
    /// <summary>
    /// Name of the operation, e.g. "FileSystemScan", "FileDatabaseUpdate", "Request:SearchCodeRequest".
    /// </summary>
    [ProtoMember(1)]
    public string Name { get; set; }
    [ProtoMember(2)]
    public long Count { get; set; }
    [ProtoMember(3)]
    public double LastMs { get; set; }
    [ProtoMember(4)]
    public double TotalMs { get; set; }
    [ProtoMember(5)]
    public double MaxMs { get; set; }
  }
}
