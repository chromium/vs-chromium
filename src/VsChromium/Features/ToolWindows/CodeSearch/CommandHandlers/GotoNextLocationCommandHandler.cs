// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch.CommandHandlers {
  [Export(typeof(IPackageCommandHandler))]
  public class GotoNextLocationCommandHandler : PackageCommandHandlerBase {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public GotoNextLocationCommandHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidGotoNextLocation);
      }
    }

    public override bool Supported {
      get {
        var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(CodeSearchToolWindow), 0, false) as CodeSearchToolWindow;
        if (window == null)
          return false;
        return window.HasNextLocation();
      }
    }

    public override void Execute(object sender, EventArgs e) {
      var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(CodeSearchToolWindow), 0, false) as CodeSearchToolWindow;
      if (window == null)
        return;
      window.NavigateToNextLocation();
    }
  }
}