// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using VsChromiumCore.Ipc;
using VsChromiumPackage.Classifier;

namespace VsChromiumTests {
  public class MefBaseTest {
    protected CompositionContainer SetupMefContainer() {
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(ITextLineChecker).Assembly));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(IpcRequest).Assembly));
      var container = new CompositionContainer(catalog);
      return container;
    }
  }
}
