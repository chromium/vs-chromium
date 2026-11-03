// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public static class VsHierarchyUtilities {
    public static readonly Guid SolutionExplorer = new Guid("{3AE79031-E1BC-11D0-8F78-00A0C9110057}");

    public static IVsUIHierarchyWindow GetSolutionExplorer(IServiceProvider serviceProvider) {
      if (serviceProvider == null)
        throw new ArgumentNullException("serviceProvider");

      var vsUiShell = serviceProvider.GetService(typeof(SVsUIShell)) as IVsUIShell;
      if (vsUiShell == null)
        throw new InvalidOperationException();

      object pvar;
      IVsWindowFrame ppWindowFrame;
      Guid persistenceSlot = SolutionExplorer;
      ErrorHandler.ThrowOnFailure(vsUiShell.FindToolWindow(0U, ref persistenceSlot, out ppWindowFrame));
      ErrorHandler.ThrowOnFailure(ppWindowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out pvar));
      return (IVsUIHierarchyWindow)pvar;
    }
  }
}