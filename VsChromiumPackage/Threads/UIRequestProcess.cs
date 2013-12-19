// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromiumCore;
using VsChromiumPackage.ServerProxy;

namespace VsChromiumPackage.Threads {
  [Export(typeof(IUIRequestProcessor))]
  public class UIRequestProcess : IUIRequestProcessor {
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;
    private readonly IDelayedOperationProcessor _delayedOperationProcessor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public UIRequestProcess(ITypedRequestProcessProxy typedRequestProcessProxy, IDelayedOperationProcessor delayedOperationProcessor, ISynchronizationContextProvider synchronizationContextProvider) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
      _delayedOperationProcessor = delayedOperationProcessor;
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    public void Post(UIRequest request) {
      if (request == null)
        throw new ArgumentNullException("request");
      if (request.Id == null)
        throw new ArgumentException("Request must have an Id.", "request");
      if (request.TypedRequest == null)
        throw new ArgumentException("Request must have a typed request.", "request");

      var operation = new DelayedOperation {
        Id = request.Id,
        Delay = request.Delay,
        Action = () => {
          if (request.OnRun != null)
            WrapActionInvocation(request.OnRun);

          _typedRequestProcessProxy.RunAsync(request.TypedRequest, response => {
            if (request.Callback != null) {
              _synchronizationContextProvider.UIContext.Post(_ => WrapActionInvocation(() => request.Callback(response)), null);
            }
          });
        }
      };

      _delayedOperationProcessor.Post(operation);
    }

    private static void WrapActionInvocation(Action action) {
      try {
        action();
      }
      catch (Exception e) {
        Logger.LogException(e, "Error calling action in UIRequestProcessor");
      }
    }
  }
}
