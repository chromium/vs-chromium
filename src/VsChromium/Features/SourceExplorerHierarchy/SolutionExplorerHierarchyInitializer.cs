// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Package;

namespace VsChromium.Features.SourceExplorerHierarchy {
  [Export(typeof(IPackagePostInitializer))]
  public class SolutionExplorerHierarchyInitializer : IPackagePostInitializer {
    private readonly ISourceExplorerHierarchyControllerFactory _sourceExplorerHierarchyControllerFactory;

    [ImportingConstructor]
    public SolutionExplorerHierarchyInitializer(
      ISourceExplorerHierarchyControllerFactory sourceExplorerHierarchyControllerFactory) {
      _sourceExplorerHierarchyControllerFactory = sourceExplorerHierarchyControllerFactory;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _sourceExplorerHierarchyControllerFactory.CreateController();
    }
  }
}