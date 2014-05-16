namespace VsChromium.Server.ProgressTracking {
  public class EnvironmentTickCountProvider : ITickCountProvider {
    public long TickCount {
      get {
        // TODO(rpaquay): Implement some logic to increment an internal counter
        // if the value rounds up.
        return System.Environment.TickCount;
      }
    }
  }
}