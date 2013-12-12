using System;
using System.ComponentModel.Composition;
using System.Reflection;

namespace VsChromiumPackage.AutoUpdate {
  [Export(typeof(IPackageVersionProvider))]
  public class PackageVersionProvider : IPackageVersionProvider {
    public Version GetVersion() {
      return Assembly.GetExecutingAssembly().GetName().Version;
    }
  }
}