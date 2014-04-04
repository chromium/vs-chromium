using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Commands;
using VsChromium.Package;
using VsChromium.Package.CommandHandler;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  [Export(typeof(IPackageCommandHandler))]
  public class ShowBuildExplorerCommandHandler
      : ShowToolWindowCommandHandler<BuildExplorerToolWindow> {

    [ImportingConstructor]
    public ShowBuildExplorerCommandHandler(IVisualStudioPackageProvider provider)
      : base(provider, PkgCmdIdList.CmdidBuildExplorerToolWindow) {
    }
  }
}
