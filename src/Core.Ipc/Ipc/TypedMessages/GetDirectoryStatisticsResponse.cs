// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class GetDirectoryStatisticsResponse : TypedResponse {
    [ProtoMember(1)]
    public int DirectoryCount { get; set; }
    [ProtoMember(2)]
    public int FileCount { get; set; }
    [ProtoMember(3)]
    public int IndexedFileCount { get; set; }
    [ProtoMember(4)]
    public long TotalIndexedFileSize { get; set; }
  }
}