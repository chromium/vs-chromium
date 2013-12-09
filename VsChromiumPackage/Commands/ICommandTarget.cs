using System.ComponentModel.Design;

namespace VsChromiumPackage.Commands {
  public interface ICommandTarget {
    bool HandlesCommand(CommandID commandId);
    bool IsEnabled(CommandID commandId);
    void Execute(CommandID commandId);
  }
}