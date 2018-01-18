// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDatabaseStatisticsResponse : TypedResponse {
    [ProtoMember(1)]
    public int ProjectCount { get; set; }
    [ProtoMember(2)]
    public long FileCount { get; set; }
    [ProtoMember(3)]
    public long IndexedFileCount { get; set; }
    [ProtoMember(4)]
    public long IndexedFileSize { get; set; }
    [ProtoMember(5)]
    public DateTime IndexLastUpdatedUtc { get; set; }
    [ProtoMember(6)]
    public bool IndexingPaused { get; set; }
    [ProtoMember(7)]
    public IndexingPausedReason IndexingPausedReason { get; set; }
  }

  public enum IndexingPausedReason {
    /// <summary>
    /// The server is actually running
    /// </summary>
    None,
    /// <summary>
    /// The server received a request to go in a "pause" state
    /// </summary>
    UserAction,
    /// <summary>
    /// The server put itself in "pause" mode because of a file system watcher buffer overflow error
    /// </summary>
    FileSystemWatcherOverflow,
  }
}