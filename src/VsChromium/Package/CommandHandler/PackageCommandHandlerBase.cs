using System;
using System.ComponentModel.Design;

namespace VsChromium.Package.CommandHandler {
  public abstract class PackageCommandHandlerBase : IPackageCommandHandler {
    public abstract CommandID CommandId { get; }
    public virtual bool Supported { get { return true; } }
    public virtual bool Enabled { get { return true; } }
    public virtual bool Visible { get { return false; } }
    public virtual bool Checked { get { return false; } }
    public abstract void Execute(object sender, EventArgs e);
  }
}