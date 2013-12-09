using System;
using System.ComponentModel.Design;

namespace VsChromiumPackage.Package.CommandHandlers {
  /// <summary>
  /// A global command handler.
  /// </summary>
  public interface IPackageCommandHandler {
    CommandID CommandId { get; }
    void Execute(object sender, EventArgs e);
  }
}