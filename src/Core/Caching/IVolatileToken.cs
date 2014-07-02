namespace VsChromium.Core.Caching {
  public interface IVolatileToken {
    bool IsCurrent { get; }
  }
}