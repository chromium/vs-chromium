using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;

namespace VsChromium.Commands {
  public class AggregateCommandTarget : ICommandTarget {
    private readonly IEnumerable<ICommandTarget> _commandTargets;

    public AggregateCommandTarget(IEnumerable<ICommandTarget> commandTargets) {
      _commandTargets = commandTargets;
    }

    public bool HandlesCommand(CommandID commandId) {
      return _commandTargets.Any(c => c.HandlesCommand(commandId));
    }

    public bool IsEnabled(CommandID commandId) {
      return true;
    }

    public void Execute(CommandID commandId) {
      _commandTargets.First(c => c.HandlesCommand(commandId)).Execute(commandId);
    }
  }
}