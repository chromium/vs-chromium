// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchFileNamesResponse : TypedResponse {
    public SearchFileNamesResponse() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      FileNames = new DirectoryEntry();
    }

    [ProtoMember(1)]
    public DirectoryEntry FileNames { get; set; }
  }
}
