// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;
using VsChromium.Core.Utility;

namespace VsChromium.Features.AttachToChrome
{
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToAllChromesHandler : PackageCommandHandlerBase
  {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToAllChromesHandler(IVisualStudioPackageProvider visualStudioPackageProvider)
    {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId { get { return new CommandID(GuidList.GuidAttachToChromeCmdSet, (int)PkgCmdIDList.CmdidAttachToAllChromes); } }

    public override void Execute(object sender, EventArgs e)
    {
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));
      foreach (EnvDTE90.Process3 p in dte.Debugger.LocalProcesses)
      {
        if (!p.IsBeingDebugged && ChromeUtility.IsChromeProcess(p.Name))
        {
          p.Attach();
        }
      }
    }
  }
}
