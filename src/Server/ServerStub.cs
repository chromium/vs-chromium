// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.Extensions.DependencyInjection;
using VsChromium.Core.Ipc;

namespace VsChromium.Server {
  public class ServerStub {
    /// <summary>
    /// Having a static instance makes debugging easier. It is not needed for anything else.
    /// </summary>
    public static IServer Instance;

    public void Run(IIpcStream stream) {
      Run(server => server.Run(stream));
    }

    private void Run(Action<IServer> runServer) {
      using (var serviceProvider = ServerServices.CreateServiceProvider()) {
        var server = serviceProvider.GetRequiredService<IServer>();
        Instance = server; // For debugging only.
        runServer(server);
      }
    }
  }
}
