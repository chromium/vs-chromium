// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using VsChromium.Server.Cache;

namespace VsChromium.Server {
  public class ServerStub {
    /// <summary>
    /// Having a static instance makes debugging easier. It is not needed for anything else.
    /// </summary>
    public static ServerComponents Instance;

    /// <summary>
    /// Runs the server (JSON-RPC protocol) until <paramref name="input"/> is closed.
    /// </summary>
    public void Run(Stream input, Stream output, IndexCacheOptions indexCacheOptions) {
      Run(input, output, new ServerOptions { IndexCache = indexCacheOptions });
    }

    /// <summary>
    /// Runs the server (JSON-RPC protocol) with <paramref name="options"/> until <paramref name="input"/> is closed.
    /// </summary>
    public void Run(Stream input, Stream output, ServerOptions options) {
      using (var components = new ServerComponents(options)) {
        Instance = components; // For debugging only.
        components.ServerHistorySampler.Start();
        components.JsonRpcServer.Run(input, output);
      }
    }
  }
}
