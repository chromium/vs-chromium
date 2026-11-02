// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using VsChromium.Core.Ipc;

namespace VsChromium.Server {
  public class ServerStub {
    /// <summary>
    /// Having a static instance makes debugging easier. It is not needed for anything else.
    /// </summary>
    public static IServer Instance;

    public void Run(int port) {
      Run(server => server.Run(port));
    }

    public void Run(IIpcStream stream) {
      Run(server => server.Run(stream));
    }

    private void Run(Action<IServer> runServer) {
      using (var mefContainer = SetupMefContainer()) {
        var server = mefContainer.GetExportedValue<IServer>();
        Instance = server; // For debugging only.
        runServer(server);
      }
    }

    private CompositionContainer SetupMefContainer() {
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(IpcRequest).Assembly));
      var container = new CompositionContainer(catalog);
      return container;
    }
  }
}
