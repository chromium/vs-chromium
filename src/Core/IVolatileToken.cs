namespace VsChromium.Server.Projects {
  public interface IVolatileToken {
    bool IsCurrent { get; }
  }
}