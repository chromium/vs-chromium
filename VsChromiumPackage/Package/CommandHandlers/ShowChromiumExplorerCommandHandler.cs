using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromiumPackage.Commands;
using VsChromiumPackage.ToolWindows.FileExplorer;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IPackageCommandHandler))]
  public class ShowChromiumExplorerCommandHandler : IPackageCommandHandler {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public ShowChromiumExplorerCommandHandler(IVisualStudioPackageProvider visualStudioPackageProvider) {
      this._visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidChromiumExplorerToolWindow);
      }
    }

    public void Execute(object sender, EventArgs e) {
      var window = this._visualStudioPackageProvider.Package.FindToolWindow(typeof(FileExplorerToolWindow), 0 /*instance id*/, true /*create*/);
      if (window == null || window.Frame == null) {
        throw new NotSupportedException("Can not create \"Chromium Explorer\" tool window.");
      }
      var windowFrame = (IVsWindowFrame)window.Frame;
      ErrorHandler.ThrowOnFailure(windowFrame.Show());
    }
  }
}