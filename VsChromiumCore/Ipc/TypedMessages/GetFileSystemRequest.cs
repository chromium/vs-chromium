// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  public class GetFileSystemRequest : TypedRequest {
    [ProtoMember(1)]
    public int KnownVersion { get; set; }
  }
}
