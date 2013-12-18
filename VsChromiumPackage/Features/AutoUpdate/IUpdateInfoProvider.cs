namespace VsChromiumPackage.Features.AutoUpdate {
  /// <summary>
  /// Abstraction over a component responsible for fetching the lastest version
  /// info of the VsChromium package.
  /// </summary>
  public interface IUpdateInfoProvider {
    UpdateInfo GetUpdateInfo();
  }
}
