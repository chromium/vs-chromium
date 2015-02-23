// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  [Export(typeof(IPackageCommandHandler))]
  public class GotoPreviousLocationCommandHandler : PackageCommandHandlerBase {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public GotoPreviousLocationCommandHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidGotoPreviousLocation);
      }
    }

    public override bool Supported {
      get {
        var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(SourceExplorerToolWindow), 0, false) as SourceExplorerToolWindow;
        if (window == null)
          return false;
        return window.HasPreviousLocation();
      }
    }

    public override void Execute(object sender, EventArgs e) {
      var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(SourceExplorerToolWindow), 0, false) as SourceExplorerToolWindow;
      if (window == null)
        return;
      window.NavigateToPreviousLocation();
    }
  }
}