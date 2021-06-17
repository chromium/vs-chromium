// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Features.ToolWindows.BuildExplorer;
using VsChromium.Features.ToolWindows.CodeSearch;
using VsChromium.Package;

namespace VsChromium.Features.ToolWindows {
  [Export(typeof(IToolWindowAccessor))]
  public class ToolWindowAccessor : IToolWindowAccessor {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public ToolWindowAccessor(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    private ToolWindow GetToolWindow<ToolWindow>(Guid guid) where ToolWindow : class {
        var uiShell = _visualStudioPackageProvider.Package.VsUIShell;
        IVsWindowFrame windowFrame;
        uiShell.FindToolWindow(
            (uint)(__VSFINDTOOLWIN.FTW_fFindFirst | __VSFINDTOOLWIN.FTW_fForceCreate),
            guid, out windowFrame);
        if (windowFrame == null)
          return null;
        windowFrame.Show();

        object docView;
        windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out docView);
        return docView as ToolWindow;
    }

    public IVsWindowFrame FindToolWindow(Guid guid) {
      var uiShell = _visualStudioPackageProvider.Package.VsUIShell;
      IVsWindowFrame windowFrame;
      uiShell.FindToolWindow(
          (uint)(__VSFINDTOOLWIN.FTW_fFindFirst),
          guid, out windowFrame);
      return windowFrame;
    }

    public CodeSearchToolWindow CodeSearch {
      get {
        return GetToolWindow<CodeSearchToolWindow>(GuidList.GuidCodeSearchToolWindow);
      }
    }

    public BuildExplorerToolWindow BuildExplorer {
      get {
        return GetToolWindow<BuildExplorerToolWindow>(GuidList.GuidBuildExplorerToolWindow);
      }
    }
  }
}
