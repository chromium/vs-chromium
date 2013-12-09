using System;
using System.ComponentModel.Design;

namespace VsChromiumPackage.Commands {
  public class SimpleCommandTarget : ICommandTarget {
    private readonly CommandID _commandId;
    private readonly Action _action;

    public SimpleCommandTarget(CommandID commandId, Action action) {
      this._commandId = commandId;
      this._action = action;
    }

    public bool HandlesCommand(CommandID commandId) {
      return this._commandId.Equals(commandId);
    }

    public bool IsEnabled(CommandID commandId) {
      return true;
    }

    public void Execute(CommandID commandId) {
      this._action();
    }
  }
}