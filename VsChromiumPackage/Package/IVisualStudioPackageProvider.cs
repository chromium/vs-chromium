
using Microsoft.VisualStudio.Shell.Interop;

namespace VsChromiumPackage.Package {
  public interface IVisualStudioPackageProvider {
    void Intialize(IVisualStudioPackage package);
    IVisualStudioPackage Package { get; }
  }
}