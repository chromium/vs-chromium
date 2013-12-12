namespace VsChromiumPackage.AutoUpdate {
  public interface IUpdateNotificationListener {
    void NotifyUpdate(UpdateInfo updateInfo);
  }
}