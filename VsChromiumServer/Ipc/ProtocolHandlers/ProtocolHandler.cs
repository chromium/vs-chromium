// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromiumCore.Ipc;

namespace VsChromiumServer.Ipc.ProtocolHandlers {
  public abstract class ProtocolHandler : IProtocolHandler {
    private readonly string _protocol;

    protected ProtocolHandler(string protocol) {
      _protocol = protocol;
    }

    public bool CanProcess(IpcRequest request) {
      return request.Protocol == _protocol;
    }

    public abstract IpcResponse Process(IpcRequest request);
  }
}
