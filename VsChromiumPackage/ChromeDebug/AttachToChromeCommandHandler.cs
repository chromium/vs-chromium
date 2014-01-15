// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Windows.Forms;
using VsChromiumPackage.Package;
using VsChromiumPackage.Package.CommandHandler;

namespace VsChromiumPackage.ChromeDebug {
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToChromeCommandHandler : IPackageCommandHandler {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToChromeCommandHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public CommandID CommandId { get { return new CommandID(GuidList.GuidChromeDebugCmdSet, (int)PkgCmdIDList.CmdidAttachToProcess); } }

    public void Execute(object sender, EventArgs e) {
      // Show a Message Box to prove we were here
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));

      var uiShell = _visualStudioPackageProvider.Package.VsUIShell;
      var parentHwnd = IntPtr.Zero;
      uiShell.GetDialogOwnerHwnd(out parentHwnd);

      var parentShim = new NativeWindow();
      parentShim.AssignHandle(parentHwnd);
      var dialog = new AttachDialog();
      var result = dialog.ShowDialog(parentShim);
      if (result == DialogResult.OK) {
        foreach (var selectedID in dialog.SelectedItems) {
          foreach (EnvDTE90.Process3 p in dte.Debugger.LocalProcesses) {
            System.Diagnostics.Debug.WriteLine("Found process {0}", p.ProcessID);
            if (p.ProcessID != selectedID)
              continue;
            p.Attach();
            System.Diagnostics.Debug.WriteLine("Attaching to process successful.");
            break;
          }
        }
      }
    }
  }
}
