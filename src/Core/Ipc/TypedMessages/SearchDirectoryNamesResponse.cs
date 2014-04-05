// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchDirectoryNamesResponse : TypedResponse {
    public SearchDirectoryNamesResponse() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      DirectoryNames = new DirectoryEntry();
    }

    [ProtoMember(1)]
    public DirectoryEntry DirectoryNames { get; set; }

    /// <summary>
    /// Total number of entries returned in |DirectoryNames|.
    /// </summary>
    [ProtoMember(2)]
    public long HitCount { get; set; }

    /// <summary>
    /// Total number of entries stored in the search index.
    /// </summary>
    [ProtoMember(3)]
    public long TotalCount { get; set; }
  }
}
