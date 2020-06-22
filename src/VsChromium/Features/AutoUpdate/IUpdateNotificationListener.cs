namespace VsChromium.Features.AutoUpdate {
  /// <summary>
  /// Components listening to "there is a new VsChromium package version
  /// available" event.
  /// </summary>
  public interface IUpdateNotificationListener {
    /// <summary>
    /// Method invoked when there is a new update available. The method is called
    /// on a background thread.
    /// </summary>
    void NotifyUpdate(UpdateInfo updateInfo);
  }
}