using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromiumPackage.Commands;
using VsChromiumPackage.ToolWindows.FileExplorer;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IChromiumExplorerToolWindowAccessor))]
  public class ChromiumExplorerToolWindowAccessor : IChromiumExplorerToolWindowAccessor {
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public ChromiumExplorerToolWindowAccessor(IVisualStudioPackageProvider visualStudioPackageProvider) {
      this._visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public void FocusSearchTextBox(CommandID commandId) {
      var toolWindow = this.GetToolWindow();
      if (toolWindow == null)
        return;

      switch (commandId.ID) {
        case PkgCmdIdList.CmdidSearchFileNames:
          toolWindow.ExplorerControl.FileNamesSearch.Focus();
          break;
        case PkgCmdIdList.CmdidSearchDirectoryNames:
          toolWindow.ExplorerControl.DirectoryNamesSearch.Focus();
          break;
        case PkgCmdIdList.CmdidSearchFileContents:
          toolWindow.ExplorerControl.FileContentsSearch.Focus();
          break;
      }
    }

    public FileExplorerToolWindow GetToolWindow() {
      var uiShell = this._visualStudioPackageProvider.Package.VsUIShell;
      IVsWindowFrame windowFrame;
      uiShell.FindToolWindow((uint)(__VSFINDTOOLWIN.FTW_fFindFirst | __VSFINDTOOLWIN.FTW_fForceCreate),
          new Guid(GuidList.GuidToolWindowPersistanceString), out windowFrame);
      if (windowFrame == null)
        return null;
      windowFrame.Show();

      object docView;
      windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out docView);
      return docView as FileExplorerToolWindow;
    }
  }
}