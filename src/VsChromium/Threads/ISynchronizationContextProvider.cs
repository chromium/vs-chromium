namespace VsChromium.Threads {
  public interface ISynchronizationContextProvider {
    ISynchronizationContext UIContext { get; }
  }
}