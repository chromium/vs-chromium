using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumPackage.Commands;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IPackageCommandHandler))]
  public class SearchFileContentsCommandHandler : IPackageCommandHandler {
    private readonly IChromiumExplorerToolWindowAccessor _toolWindowAccessor;

    [ImportingConstructor]
    public SearchFileContentsCommandHandler(IChromiumExplorerToolWindowAccessor toolWindowAccessor) {
      this._toolWindowAccessor = toolWindowAccessor;
    }

    public CommandID CommandId {
      get {
        return new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidSearchFileContents);
      }
    }

    public void Execute(object sender, EventArgs e) {
      this._toolWindowAccessor.FocusSearchTextBox(this.CommandId);
    }
  }
}