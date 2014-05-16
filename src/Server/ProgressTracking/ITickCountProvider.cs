namespace VsChromium.Server.ProgressTracking {
  public interface ITickCountProvider {
    /// <returns>
    /// Returns a integer value containing the amount of time in milliseconds that has passed since some arbitrary initial moment.
    /// </returns>
    long TickCount { get; }
  }
}