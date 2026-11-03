using System;
using System.ComponentModel.Composition;
using System.Reflection;

namespace VsChromium.Features.AutoUpdate {
  /// <summary>
  /// Return the VsChromium package version by looking up the assembly version.
  /// </summary>
  [Export(typeof(IPackageVersionProvider))]
  public class PackageVersionProvider : IPackageVersionProvider {
    public Version GetVersion() {
      return Assembly.GetExecutingAssembly().GetName().Version;
    }
  }
}