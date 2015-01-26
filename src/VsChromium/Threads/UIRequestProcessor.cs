// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.ServerProxy;

namespace VsChromium.Threads {
  [Export(typeof(IUIRequestProcessor))]
  public class UIRequestProcessor : IUIRequestProcessor {
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;
    private readonly IDelayedOperationProcessor _delayedOperationProcessor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public UIRequestProcessor(ITypedRequestProcessProxy typedRequestProcessProxy,
                              IDelayedOperationProcessor delayedOperationProcessor,
                              ISynchronizationContextProvider synchronizationContextProvider) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
      _delayedOperationProcessor = delayedOperationProcessor;
      _synchronizationContextProvider = synchronizationContextProvider;
    }

    public void Post(UIRequest request) {
      if (request == null)
        throw new ArgumentNullException("request");
      if (request.Id == null)
        throw new ArgumentException(@"Request must have an Id.", "request");
      if (request.Request == null)
        throw new ArgumentException(@"Request must have a typed request.", "request");

      var operation = new DelayedOperation {
        Id = request.Id,
        Delay = request.Delay,
        // Action executed on a background thread
        Action = () => {
          if (request.OnSend != null)
            Logger.WrapActionInvocation(request.OnSend);

          _typedRequestProcessProxy.RunAsync(request.Request,
            response => OnRequestSuccess(request, response),
            errorResponse => OnRequestError(request, errorResponse));
        }
      };

      _delayedOperationProcessor.Post(operation);
    }

    private void OnRequestSuccess(UIRequest request, TypedResponse response) {
      if (request.OnSuccess != null || request.OnReceive != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (request.OnReceive != null)
            request.OnReceive();
          if (request.OnSuccess != null)
            request.OnSuccess(response);
        });
      }
    }

    private void OnRequestError(UIRequest request, ErrorResponse errorResponse) {
      if (request.OnError != null || request.OnReceive != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (request.OnReceive != null)
            request.OnReceive();
          if (request.OnError != null)
            if (errorResponse.IsOperationCanceled()) {
              // UIRequest are cancelable at any point.
            } else {
              request.OnError(errorResponse);
            }
        });
      }
    }
  }
}
