using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Linq;

namespace VsChromiumPackage.Threads {
  [Export(typeof(IDelayedOperationProcessor))]
  public class DelayedOperationProcessor : IDelayedOperationProcessor {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AutoResetEvent _event = new AutoResetEvent(false);
    private readonly object _lock = new object();
    private readonly LinkedList<Entry> _requests = new LinkedList<Entry>();

    [ImportingConstructor]
    public DelayedOperationProcessor(IDateTimeProvider dateTimeProvider) {
      _dateTimeProvider = dateTimeProvider;
      new Thread(ThreadLoop) { IsBackground = true }.Start();
    }

    public void Post(DelayedOperation operation) {
      if (operation == null)
        throw new ArgumentNullException("operation");
      if (operation.Action == null)
        throw new ArgumentException("Delayed operation must have a callback.", "operation");
      if (operation.Id == null)
        throw new ArgumentException("Delayed operation must have an Id.", "operation");

      lock (_lock) {
        // TODO(rpaquay): Consider a more efficient way if this becomes a bottleneck
        for (var node = _requests.First; node != null; node = node.Next) {
          if (node.Value.DelayedOperation.Id == operation.Id)
            _requests.Remove(node);
        }
        _requests.AddLast(new Entry {
          DateEnqeued = _dateTimeProvider.UtcNow,
          DelayedOperation = operation,
        });
        _event.Set();
      }
    }

    private void ThreadLoop() {
      try {
        while (true) {
          _event.WaitOne(10);
          var requestsToExecute = new List<DelayedOperation>();
          lock (_lock) {
            // TODO(rpaquay): Consider a more efficient way if this becomes a bottleneck
            for (var node = _requests.First; node != null; node = node.Next) {
              if (node.Value.DateEnqeued + node.Value.DelayedOperation.Delay <= _dateTimeProvider.UtcNow) {
                requestsToExecute.Add(node.Value.DelayedOperation);
                _requests.Remove(node);
              }
            }
          }
          requestsToExecute.ForAll(request => WrapActionInvocation(request.Action));
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in DelayedOperationProcessor.");
      }
    }

    private static void WrapActionInvocation(Action action) {
      try {
        action();
      }
      catch (Exception e) {
        Logger.LogException(e, "Error calling action in DelayedOperationProcessor");
      }
    }

    private class Entry {
      public DateTime DateEnqeued { get; set; }
      public DelayedOperation DelayedOperation { get; set; }
    }
  }
}