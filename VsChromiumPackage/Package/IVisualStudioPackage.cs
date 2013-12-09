using System;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace VsChromiumPackage.Package {
  public interface IVisualStudioPackage {
    IComponentModel ComponentModel { get; }
    OleMenuCommandService OleMenuCommandService { get; }
    IVsUIShell VsUIShell { get; }

    ToolWindowPane FindToolWindow(Type toolWindowType, int id, bool create);
  }
}