// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using VsChromium.Core.Ipc;

namespace VsChromium.Server {
  public class ServerStub {
    /// <summary>
    /// Having a static instance makes debugging easier. It is not needed for anything else.
    /// </summary>
    public static ServerComponents Instance;

    /// <summary>
    /// Runs the server with the typed messages protocol ("--stdio") until <paramref name="stream"/> is closed.
    /// </summary>
    public void Run(IIpcStream stream) {
      Run(ServerProtocolMode.TypedMessages, components => components.Server.Run(stream));
    }

    /// <summary>
    /// Runs the server with the JSON-RPC protocol ("--jsonrpc") until <paramref name="input"/> is closed.
    /// </summary>
    public void RunJsonRpc(Stream input, Stream output) {
      Run(ServerProtocolMode.JsonRpc, components => components.JsonRpcServer.Run(input, output));
    }

    private void Run(ServerProtocolMode mode, Action<ServerComponents> runServer) {
      using (var components = new ServerComponents(mode)) {
        Instance = components; // For debugging only.
        runServer(components);
      }
    }
  }
}
