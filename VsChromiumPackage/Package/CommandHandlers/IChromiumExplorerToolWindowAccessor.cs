using System.ComponentModel.Design;
using VsChromiumPackage.ToolWindows.FileExplorer;

namespace VsChromiumPackage.Package.CommandHandlers {
  public interface IChromiumExplorerToolWindowAccessor {
    FileExplorerToolWindow GetToolWindow();
    void FocusSearchTextBox(CommandID commandId);
  }
}