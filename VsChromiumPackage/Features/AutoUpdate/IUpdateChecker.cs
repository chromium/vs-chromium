namespace VsChromiumPackage.Features.AutoUpdate {
  /// <summary>
  /// Abstraction over the "new version available" periodic checks.
  /// </summary>
  public interface IUpdateChecker {
    /// <summary>
    /// Start the update checker by queueing delayed task for periodical checks.
    /// </summary>
    void Start();
  }
}