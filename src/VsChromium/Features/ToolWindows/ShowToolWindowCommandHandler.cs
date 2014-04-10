// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.ComponentModel.Design;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows {
  public class ShowToolWindowCommandHandler<ToolWindow> : IPackageCommandHandler {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;
    private readonly int _cmdid;

    public ShowToolWindowCommandHandler(
        IVisualStudioPackageProvider visualStudioPackageProvider, int cmdid) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
      _cmdid = cmdid;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidVsChromiumCmdSet, _cmdid); } }

    public void Execute(object sender, EventArgs e) {
      var window = _visualStudioPackageProvider.Package.FindToolWindow(typeof(ToolWindow), 0 /*instance id*/, true /*create*/);
      if (window == null || window.Frame == null) {
        throw new NotSupportedException("Can not create \"Chromium Source Explorer\" tool window.");
      }
      var windowFrame = (IVsWindowFrame)window.Frame;
      ErrorHandler.ThrowOnFailure(windowFrame.Show());
    }
  }
}
