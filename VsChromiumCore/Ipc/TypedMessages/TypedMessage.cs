// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc.TypedMessages {
  [ProtoContract]
  [ProtoInclude(10, typeof(TypedRequest))]
  [ProtoInclude(11, typeof(TypedResponse))]
  [ProtoInclude(12, typeof(TypedEvent))]
  public class TypedMessage : IpcMessageData {
    public TypedMessage() {
      ClassName = GetType().Name;
    }

    [ProtoMember(1)]
    public string ClassName { get; set; }
  }
}
