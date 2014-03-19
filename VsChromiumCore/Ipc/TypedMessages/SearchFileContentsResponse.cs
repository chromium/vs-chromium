// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc.TypedMessages {
  [ProtoContract]
  public class SearchFileContentsResponse : TypedResponse {
    public SearchFileContentsResponse() {
      // To avoid getting "null" property if empty collection deserialized using protobuf.
      SearchResults = new DirectoryEntry();
    }

    [ProtoMember(1)]
    public DirectoryEntry SearchResults { get; set; }
  }
}
