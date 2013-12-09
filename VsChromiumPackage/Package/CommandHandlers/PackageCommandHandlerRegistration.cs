using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using VsChromiumCore;

namespace VsChromiumPackage.Package.CommandHandlers {
  [Export(typeof(IPackageCommandHandlerRegistration))]
  public class PackageCommandHandlerRegistration : IPackageCommandHandlerRegistration {
    private readonly IEnumerable<IPackageCommandHandler> _commandHandlers;
    private readonly IVisualStudioPackageProvider _visualStudioPackageProvider;

    [ImportingConstructor]
    public PackageCommandHandlerRegistration([ImportMany] IEnumerable<IPackageCommandHandler> commandHandlers, IVisualStudioPackageProvider visualStudioPackageProvider) {
      this._commandHandlers = commandHandlers;
      this._visualStudioPackageProvider = visualStudioPackageProvider;
    }

    public void RegisterCommandHandlers() {
      var mcs = this._visualStudioPackageProvider.Package.OleMenuCommandService;
      if (mcs == null) {
        Logger.LogError("Error getting instance of OleMenuCommandService");
        return;
      }

      foreach (var handler in this._commandHandlers) {
        // Create the command for the tool window
        var command = new MenuCommand(handler.Execute, handler.CommandId);
        mcs.AddCommand(command);
      }
    }
  }
}