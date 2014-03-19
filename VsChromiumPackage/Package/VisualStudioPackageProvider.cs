// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;

namespace VsChromium.Package {
  [Export(typeof(IVisualStudioPackageProvider))]
  public class VisualStudioPackageProvider : IVisualStudioPackageProvider {
    private IVisualStudioPackage _package;

    public void SetPackage(IVisualStudioPackage package) {
      if (_package != null)
        throw new InvalidOperationException("Package singleton already set.");
      _package = package;
    }

    public IVisualStudioPackage Package {
      get {
        if (_package == null)
          throw new InvalidOperationException("Package singleton not set. Call Initialize() method.");
        return _package;
      }
    }
  }
}
