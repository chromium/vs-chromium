// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using ProtoBuf;

namespace VsChromiumCore.Ipc {
  [ProtoContract]
  public class ErrorResponse : IpcMessageData {
    [ProtoMember(1)]
    public string Message { get; set; }

    [ProtoMember(2)]
    public string FullTypeName { get; set; }

    [ProtoMember(3)]
    public string StackTrace { get; set; }

    [ProtoMember(4)]
    public ErrorResponse InnerError { get; set; }
  }
}
