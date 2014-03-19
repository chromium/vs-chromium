// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromium.Core.Ipc {
  [ProtoContract]
  [ProtoInclude(10, typeof(IpcRequest))]
  [ProtoInclude(11, typeof(IpcResponse))]
  public class IpcMessage {
    [ProtoMember(1)]
    public long RequestId { get; set; }

    [ProtoMember(2)]
    public string Protocol { get; set; }

    [ProtoMember(3)]
    public IpcMessageData Data { get; set; }
  }
}
