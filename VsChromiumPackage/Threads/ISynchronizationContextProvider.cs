using System.Threading;

namespace VsChromiumPackage.Threads {
  public interface ISynchronizationContextProvider {
    SynchronizationContext UIContext { get; }
  }
}