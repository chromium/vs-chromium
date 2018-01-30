// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Package;
using VsChromium.Threads;

namespace VsChromium.Features.SourceExplorerHierarchy {
  [Export(typeof(IPackagePostInitializer))]
  public class SolutionExplorerHierarchyInitializer : IPackagePostInitializer {
    private readonly ISourceExplorerHierarchyControllerFactory _sourceExplorerHierarchyControllerFactory;
    private readonly IDispatchThreadDelayedOperationExecutor _dispatchThreadDelayedOperationExecutor;

    [ImportingConstructor]
    public SolutionExplorerHierarchyInitializer(
      ISourceExplorerHierarchyControllerFactory sourceExplorerHierarchyControllerFactory,
      IDispatchThreadDelayedOperationExecutor dispatchThreadDelayedOperationExecutor) {
      _sourceExplorerHierarchyControllerFactory = sourceExplorerHierarchyControllerFactory;
      _dispatchThreadDelayedOperationExecutor = dispatchThreadDelayedOperationExecutor;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _dispatchThreadDelayedOperationExecutor.Post(new DelayedOperation {
        Id = "SolutionExplorerHierarchyInitializer",
        Delay = TimeSpan.FromSeconds(2.0),
        Action = () => {
          var controller = _sourceExplorerHierarchyControllerFactory.CreateController();
          controller.Activate();
        }
      });
    }
  }
}