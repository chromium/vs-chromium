// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using VsChromiumCore.Ipc;

namespace VsChromiumServer {
  public class ServerStub {
    /// <summary>
    /// Having a static instance makes debugging easier. It is not needed for anything else.
    /// </summary>
    public static IServer Instance;

    public void Run(int port) {
      using (var mefContainer = SetupMefContainer()) {
        var server = mefContainer.GetExport<IServer>().Value;
        Instance = server; // For debugging only.
        server.Run(port);
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
