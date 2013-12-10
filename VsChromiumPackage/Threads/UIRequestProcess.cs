// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Linq;
using VsChromiumPackage.Server;

namespace VsChromiumPackage.Threads {
  [Export(typeof(IUIRequestProcessor))]
  public class UIRequestProcess : IUIRequestProcessor {
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AutoResetEvent _event = new AutoResetEvent(false);
    private readonly object _lock = new object();
    private readonly LinkedList<Entry> _requests = new LinkedList<Entry>();
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;
    private readonly SynchronizationContext _uiSynchronizationContext;

    [ImportingConstructor]
    public UIRequestProcess(ITypedRequestProcessProxy typedRequestProcessProxy, IDateTimeProvider dateTimeProvider) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
      _dateTimeProvider = dateTimeProvider;
      _uiSynchronizationContext = SynchronizationContext.Current;
      new Thread(ThreadLoop) {IsBackground = true}.Start();
    }

    public void Post(UIRequest request) {
      lock (_lock) {
        // TODO(rpaquay): Consider a more efficient way if this becomes a bottleneck
        for (var node = _requests.First; node != null; node = node.Next) {
          if (node.Value.UIRequest.Id == request.Id)
            _requests.Remove(node);
        }
        _requests.AddLast(new Entry {
          DateEnqeued = _dateTimeProvider.UtcNow,
          UIRequest = request,
        });
        _event.Set();
      }
    }

    private void ThreadLoop() {
      try {
        while (true) {
          _event.WaitOne(10);
          var requestsToExecute = new List<UIRequest>();
          lock (_lock) {
            // TODO(rpaquay): Consider a more efficient way if this becomes a bottleneck
            for (var node = _requests.First; node != null; node = node.Next) {
              if (node.Value.DateEnqeued + node.Value.UIRequest.Delay <= _dateTimeProvider.UtcNow) {
                requestsToExecute.Add(node.Value.UIRequest);
                _requests.Remove(node);
              }
            }
          }
          requestsToExecute.ForAll(request => {
            if (request.OnRun != null)
              WrapActionInvocation(request.OnRun);

            _typedRequestProcessProxy.RunAsync(request.TypedRequest, response => {
              if (request.Callback != null) {
                _uiSynchronizationContext.Post(_ => WrapActionInvocation(() => request.Callback(response)), null);
              }
            });
          });
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in UI Request processor.");
      }
    }

    private static void WrapActionInvocation(Action action) {
      try {
        action();
      }
      catch (Exception e) {
        Logger.LogException(e, "Error calling action in UIRequestProcessor");
      }
    }

    private class Entry {
      public DateTime DateEnqeued { get; set; }
      public UIRequest UIRequest { get; set; }
    }
  }
}
