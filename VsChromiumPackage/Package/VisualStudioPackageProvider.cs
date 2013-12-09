using System;
using System.ComponentModel.Composition;

namespace VsChromiumPackage.Package {
  [Export(typeof(IVisualStudioPackageProvider))]
  public class VisualStudioPackageProvider : IVisualStudioPackageProvider {
    private IVisualStudioPackage _package;

    public void Intialize(IVisualStudioPackage package) {
      if (this._package != null)
        throw new InvalidOperationException("Package singleton already set.");
      this._package = package;
    }

    public IVisualStudioPackage Package {
      get {
        if (this._package == null)
          throw new InvalidOperationException("Package singleton not set. Call Initialize() method.");
        return this._package;
      }
    }
  }
}