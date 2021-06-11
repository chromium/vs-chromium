// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Package {
  [Export(typeof(IPackagePreInitializer))]
  public class VisualStudioPackageInitializer : IPackagePreInitializer {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public VisualStudioPackageInitializer(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public int Priority { get { return int.MaxValue; } }

    public void Run(IVisualStudioPackage package) {
      _visualStudioPackageProvider.SetPackage(package);
    }
  }
}