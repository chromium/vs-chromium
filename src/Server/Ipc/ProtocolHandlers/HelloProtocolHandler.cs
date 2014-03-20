// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;

namespace VsChromium.Server.Ipc.ProtocolHandlers {
  [Export(typeof(IProtocolHandler))]
  public class HelloProtocolHandler : ProtocolHandler {
    public HelloProtocolHandler()
      : base(IpcProtocols.Hello) {
    }

    public override IpcResponse Process(IpcRequest request) {
      return HelloWorldProtocol.Response;
    }
  }
}
