// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  [Export(typeof(IPackagePriorityCommandHandler))]
  public class GlobalPreviousLocationCommandHandler : PackagePriorityCommandHandlerBase {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public GlobalPreviousLocationCommandHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId {
      get {
        return new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.PreviousLocation);
      }
    }

    public override bool Supported {
      get {
        var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(CodeSearchToolWindow), 0, false) as CodeSearchToolWindow;
        if (window == null)
          return false;
        if (!window.IsVisible)
          return false;
        return window.HasPreviousLocation();
      }
    }

    public override void Execute(object sender, EventArgs e) {
      var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(CodeSearchToolWindow), 0, false) as CodeSearchToolWindow;
      if (window == null)
        return;
      window.NavigateToPreviousLocation();
    }
  }
}