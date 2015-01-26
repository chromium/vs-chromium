using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using VsChromium.Commands;

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class NextLocationCommandHandler : ICommandTarget {
    private readonly SourceExplorerToolWindow _window;
    private readonly CommandID _commandId;

    public NextLocationCommandHandler(SourceExplorerToolWindow window) {
      _window = window;
      _commandId = new CommandID(VSConstants.GUID_VSStandardCommandSet97, (int)VSConstants.VSStd97CmdID.NextLocation);
    }

    public bool HandlesCommand(CommandID commandId) {
      return _commandId.Equals(commandId);
    }

    public bool IsEnabled(CommandID commandId) {
      return _window.HasNextLocation();
    }

    public void Execute(CommandID commandId) {
      _window.NavigateToNextLocation();
    }
  }
}