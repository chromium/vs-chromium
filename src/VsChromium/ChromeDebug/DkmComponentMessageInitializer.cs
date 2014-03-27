// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Package;

namespace VsChromium.ChromeDebug {
  [Export(typeof(IPackagePostInitializer))]
  class DkmComponentMessageInitializer : IPackagePostInitializer {
    public int Priority {
      get { return 0; }
    }

    public void Run(IVisualStudioPackage package) {
      // Register handler to receive custom messages from the Dkm components.
      DkmComponentEventHandler handler = new DkmComponentEventHandler();
      IServiceContainer container = (IServiceContainer)package;
      container.AddService(handler.GetType(), handler, true);
    }
  }
}
