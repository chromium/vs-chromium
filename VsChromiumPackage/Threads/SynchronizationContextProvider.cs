using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromium.Threads {
  [Export(typeof(ISynchronizationContextProvider))]
  public class SynchronizationContextProvider : ISynchronizationContextProvider {
    private readonly SynchronizationContext _context;

    public SynchronizationContextProvider() {
      _context = SynchronizationContext.Current;
    }
    public SynchronizationContext UIContext { get { return _context; } }
  }
}