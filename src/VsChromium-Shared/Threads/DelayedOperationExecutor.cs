// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Threads {
  [Export(typeof(IDelayedOperationExecutor))]
  public class DelayedOperationExecutor : IDelayedOperationExecutor {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AutoResetEvent _event = new AutoResetEvent(false);
    private readonly object _lock = new object();
    private readonly LinkedList<Entry> _requests = new LinkedList<Entry>();

    [ImportingConstructor]
    public DelayedOperationExecutor(IDateTimeProvider dateTimeProvider) {
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
          requestsToExecute.ForAll(request => Logger.WrapActionInvocation(request.Action));
        }
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in DelayedOperationProcessor.");
      }
    }

    private class Entry {
      public DateTime DateEnqeued { get; set; }
      public DelayedOperation DelayedOperation { get; set; }
    }
  }
}