// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromium.Package;

namespace VsChromium.Features.AttachToChrome {
  [Export(typeof(IPackagePostInitializer))]
  class DkmComponentMessageInitializer : IPackagePostInitializer {
    public int Priority {
      get { return 0; }
    }

    public void Run(IVisualStudioPackage package) {
      // Register handler to receive custom messages from the Dkm components.
      var handler = new DkmComponentEventHandler(package);
      var container = (IServiceContainer)package;
      container.AddService(handler.GetType(), handler, true);
    }
  }
}
