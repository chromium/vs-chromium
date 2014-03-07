// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumPackage.Package;
using VsChromiumPackage.Package.CommandHandler;
using VsChromiumCore.Utility;
using VsChromiumCore.Processes;


namespace VsChromiumPackage.ChromeDebug
{
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToChromeDescendantsHandler : IPackageCommandHandler
  {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToChromeDescendantsHandler(IVisualStudioPackageProvider visualStudioPackageProvider)
    {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidChromeDebugCmdSet, (int)PkgCmdIDList.CmdidAttachToDescendants); } }

    public void Execute(object sender, EventArgs e)
    {
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));

      HashSet<int> roots = new HashSet<int>();
      foreach (EnvDTE90.Process3 p in dte.Debugger.DebuggedProcesses) {
        if (p.IsBeingDebugged && ChromeUtility.IsChromeProcess(p.Name))
          roots.Add(p.ProcessID);
      }

      foreach (EnvDTE90.Process3 p in dte.Debugger.LocalProcesses)
      {
        System.Diagnostics.Debug.WriteLine("Found process {0}", p.ProcessID);
        if (p.IsBeingDebugged || !ChromeUtility.IsChromeProcess(p.Name))
          continue;

        using (NtProcess process = new NtProcess(p.ProcessID)) {
          if (!roots.Contains(process.ParentProcessId))
            continue;

          p.Attach();
          System.Diagnostics.Debug.WriteLine("Attaching to process successful.");
        }
      }
    }
  }
}
