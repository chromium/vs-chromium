// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Ipc.TypedMessages {
  public class GetDatabaseStatisticsResponse : TypedResponse {
    public int ProjectCount { get; set; }
    public long FileCount { get; set; }
    public long SearchableFileCount { get; set; }
    public long ServerNativeMemoryUsage { get; set; }
    public long ServerGcMemoryUsage { get; set; }
    public DateTime IndexLastUpdatedUtc { get; set; }
    public IndexingServerStatus ServerStatus { get; set; }
  }

  public enum IndexingServerStatus {
    /// <summary>
    /// The server is actually running
    /// </summary>
    Idle,
    /// <summary>
    /// The server received a request to go in a "pause" state
    /// </summary>
    Paused,
    /// <summary>
    /// The server put itself in "pause" mode because of a file system watcher buffer overflow error
    /// </summary>
    Yield,
    /// <summary>
    /// The server is busy indexing the file system
    /// </summary>
    Busy,
  }
}
