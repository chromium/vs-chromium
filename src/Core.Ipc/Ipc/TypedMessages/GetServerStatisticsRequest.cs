// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  /// <summary>
  /// Returns statistics about the resource usage of the server process (CPU, memory, timings of
  /// the main operations), as measured inside the server. Used by benchmarks and diagnostics.
  /// </summary>
  [ProtoContract]
  public class GetServerStatisticsRequest : TypedRequest {
    /// <summary>
    /// Force a full garbage collection before collecting statistics.
    /// </summary>
    [ProtoMember(1)]
    public bool ForceGarbageCollection { get; set; }
  }
}
