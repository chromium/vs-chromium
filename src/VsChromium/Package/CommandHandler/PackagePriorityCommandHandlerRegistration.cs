using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;

namespace VsChromium.Package.CommandHandler {
  [Export(typeof(IPackagePostInitializer))]
  public class PackagePriorityCommandHandlerRegistration : IPackagePostInitializer {
    private readonly IEnumerable<IPackagePriorityCommandHandler> _commandHandlers;

    [ImportingConstructor]
    public PackagePriorityCommandHandlerRegistration([ImportMany] IEnumerable<IPackagePriorityCommandHandler> commandHandlers) {
      _commandHandlers = commandHandlers;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      var commandService = new OleMenuCommandService(package.ServiceProvider);
      var registerPriorityCommandTarget = package.VsRegisterPriorityCommandTarget;
      uint cookie;
      int hr = registerPriorityCommandTarget.RegisterPriorityCommandTarget(0, commandService, out cookie);
      try {
        ErrorHandler.ThrowOnFailure(hr);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error registering priority command handler.");
        return;
      }

      package.DisposeContainer.Add(() => registerPriorityCommandTarget.UnregisterPriorityCommandTarget(cookie));

      foreach (var handler in _commandHandlers) {
        // Create the command for the tool window
        var capturedHandler = handler;
        var command = new OleMenuCommand(handler.Execute, handler.CommandId);
        command.BeforeQueryStatus += (sender, args) => {
          command.Supported = capturedHandler.Supported;
          command.Checked = capturedHandler.Checked;
          command.Enabled = capturedHandler.Enabled;
          command.Visible = capturedHandler.Visible;
        };
        commandService.AddCommand(command);
      }
    }
  }
}