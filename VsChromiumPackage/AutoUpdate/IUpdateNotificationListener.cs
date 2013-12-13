namespace VsChromiumPackage.AutoUpdate {
  /// <summary>
  /// Components listening to "there is a new VsChromium package version
  /// available" event.
  /// </summary>
  public interface IUpdateNotificationListener {
    void NotifyUpdate(UpdateInfo updateInfo);
  }
}