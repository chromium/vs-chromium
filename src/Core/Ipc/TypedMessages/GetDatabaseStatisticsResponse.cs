// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDatabaseStatisticsResponse : TypedResponse {
    [ProtoMember(1)]
    public int ProjectCount { get; set; }
    [ProtoMember(2)]
    public long DirectoryCount { get; set; }
    [ProtoMember(3)]
    public long FileCount { get; set; }
    [ProtoMember(4)]
    public long IndexedFileCount { get; set; }
    [ProtoMember(5)]
    public long IndexedFileSize { get; set; }
  }
}