// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition.Hosting;
using System.Reflection;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.ProtoBuf;
using VsChromium.Features.ChromiumCodingStyleChecker;
using VsChromium.Server.Search;

namespace VsChromium.Tests {
  public class MefTestBase {
    protected static CompositionContainer SetupMefContainer() {
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(ITextLineChecker).Assembly));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(ProtoBufSerializer).Assembly)); // VsChromium.Core
      var container = new CompositionContainer(catalog);
      return container;
    }
    protected static CompositionContainer SetupServerMefContainer() {
      var catalog = new AggregateCatalog();
      catalog.Catalogs.Add(new AssemblyCatalog(Assembly.GetExecutingAssembly()));
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(ProtoBufSerializer).Assembly)); // VsChromium.Core
      catalog.Catalogs.Add(new AssemblyCatalog(typeof(SearchEngine).Assembly));
      var container = new CompositionContainer(catalog);
      return container;
    }
  }
}
