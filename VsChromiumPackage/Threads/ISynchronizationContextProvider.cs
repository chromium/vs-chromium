using System.Threading;

namespace VsChromium.Threads {
  public interface ISynchronizationContextProvider {
    SynchronizationContext UIContext { get; }
  }
}