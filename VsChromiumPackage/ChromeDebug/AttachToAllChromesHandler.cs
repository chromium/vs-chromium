// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Windows.Forms;
using VsChromiumPackage.Package;
using VsChromiumPackage.Package.CommandHandlers;

namespace VsChromiumPackage.ChromeDebug
{
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToAllChromesHandler : IPackageCommandHandler
  {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToAllChromesHandler(IVisualStudioPackageProvider visualStudioPackageProvider)
    {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidChromeDebugCmdSet, (int)PkgCmdIDList.CmdidAttachToAllChromes); } }

    public void Execute(object sender, EventArgs e)
    {
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));
      foreach (EnvDTE90.Process3 p in dte.Debugger.LocalProcesses)
      {
        System.Diagnostics.Debug.WriteLine("Found process {0}", p.ProcessID);
        if (!p.IsBeingDebugged && Utility.IsChromeProcess(p.Name))
        {
          p.Attach();
          System.Diagnostics.Debug.WriteLine("Attaching to process successful.");
        }
      }
    }
  }
}
