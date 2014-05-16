using System.ComponentModel.Composition;
using System.Threading;

namespace VsChromium.Server.Operations {
  [Export(typeof(IOperationIdFactory))]
  public class OperationIdFactory : IOperationIdFactory {
    private long _nextId = 1;

    public long GetNextId() {
      return Interlocked.Increment(ref _nextId);
    }
  }
}