// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumCore.Ipc {
  [ProtoContract]
  [ProtoInclude(10, typeof(IpcStringData))]
  [ProtoInclude(11, typeof(TypedMessage))]
  [ProtoInclude(12, typeof(ExceptionData))]
  public class IpcMessageData {
  }
}
