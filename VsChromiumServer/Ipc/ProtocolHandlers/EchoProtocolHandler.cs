// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Ipc;

namespace VsChromiumServer.Ipc.ProtocolHandlers {
  [Export(typeof(IProtocolHandler))]
  public class EchoProtocolHandler : ProtocolHandler {
    public EchoProtocolHandler()
        : base(IpcProtocols.Echo) {
    }

    public override IpcResponse Process(IpcRequest request) {
      return new IpcResponse {
        RequestId = request.RequestId,
        Protocol = request.Protocol,
        Data = request.Data
      };
    }
  }
}
