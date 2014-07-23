// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;
using VsChromium.Core.Utility;
using VsChromium.Core.Processes;


namespace VsChromium.Features.AttachToChrome
{
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToChromeDescendantsHandler : PackageCommandHandlerBase
  {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToChromeDescendantsHandler(IVisualStudioPackageProvider visualStudioPackageProvider)
    {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId { get { return new CommandID(GuidList.GuidAttachToChromeCmdSet, (int)PkgCmdIDList.CmdidAttachToDescendants); } }

    public override void Execute(object sender, EventArgs e)
    {
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));

      HashSet<int> roots = new HashSet<int>();
      foreach (EnvDTE90.Process3 p in dte.Debugger.DebuggedProcesses) {
        if (p.IsBeingDebugged && ChromeUtility.IsChromeProcess(p.Name))
          roots.Add(p.ProcessID);
      }

      foreach (EnvDTE90.Process3 p in dte.Debugger.LocalProcesses)
      {
        if (p.IsBeingDebugged || !ChromeUtility.IsChromeProcess(p.Name))
          continue;

        NtProcess process = new NtProcess(p.ProcessID);
        if (!roots.Contains(process.ParentProcessId))
          continue;

        p.Attach();
      }
    }
  }
}
