// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Windows.Forms;
using VsChromium.Core.DkmShared;
using VsChromium.Core.Processes;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.AttachToChrome {
  [Export(typeof(IPackageCommandHandler))]
  public class AttachToChromeDialogHandler : PackageCommandHandlerBase {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public AttachToChromeDialogHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      _visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public override CommandID CommandId { get { return new CommandID(GuidList.GuidAttachToChromeCmdSet, (int)PkgCmdIDList.CmdidAttachToChromeDialog); } }

    public override void Execute(object sender, EventArgs e) {
      var dte = (EnvDTE.DTE)_visualStudioPackageProvider.Package.DTE; //GetService(typeof(EnvDTE.DTE));

      var uiShell = _visualStudioPackageProvider.Package.VsUIShell;
      var parentHwnd = IntPtr.Zero;
      uiShell.GetDialogOwnerHwnd(out parentHwnd);

      var parentShim = new NativeWindow();
      parentShim.AssignHandle(parentHwnd);
      var dialog = new AttachDialog();
      var result = dialog.ShowDialog(parentShim);
      if (result == DialogResult.OK) {
        HashSet<Process> processes = new HashSet<Process>();
        foreach (int pid in dialog.SelectedItems) {
          Process p = Process.GetProcessById(pid);
          if (!p.IsBeingDebugged())
            processes.Add(p);

          if (dialog.AutoAttachToCurrentChildren) {
            foreach (Process child in p.GetChildren()) {
              if (!child.IsBeingDebugged())
                processes.Add(child);
            }
          }
        }
        List<Process> processList = new List<Process>(processes);
        ChildDebuggingMode mode = (dialog.AutoAttachToFutureChildren)
            ? ChildDebuggingMode.AlwaysAttach
            : ChildDebuggingMode.UseDefault;
        DebugAttach.AttachToProcess(processList.ToArray(), mode);
      }
    }
  }
}
