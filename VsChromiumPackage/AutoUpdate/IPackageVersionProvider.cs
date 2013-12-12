using System;

namespace VsChromiumPackage.AutoUpdate {
  public interface IPackageVersionProvider {
    Version GetVersion();
  }
}