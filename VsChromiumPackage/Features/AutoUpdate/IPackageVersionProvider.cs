using System;

namespace VsChromiumPackage.Features.AutoUpdate {
  /// <summary>
  /// Provides the version of the VsChromium package instance.
  /// </summary>
  public interface IPackageVersionProvider {
    Version GetVersion();
  }
}