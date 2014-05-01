// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using VsChromium.Core;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
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
      if (request.TypedRequest == null)
        throw new ArgumentException(@"Request must have a typed request.", "request");

      var operation = new DelayedOperation {
        Id = request.Id,
        Delay = request.Delay,
        Action = () => {
          if (request.OnBeforeRun != null)
            Logger.WrapActionInvocation(request.OnBeforeRun);

          _typedRequestProcessProxy.RunAsync(request.TypedRequest,
            response => OnRequestSuccess(request, response),
            errorResponse => OnRequestError(request, errorResponse));
        }
      };

      _delayedOperationProcessor.Post(operation);
    }

    private void OnRequestSuccess(UIRequest request, TypedResponse response) {
      if (request.SuccessCallback != null || request.OnAfterRun != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (request.OnAfterRun != null)
            request.OnAfterRun();
          if (request.SuccessCallback != null)
            request.SuccessCallback(response);
        });
      }
    }

    private void OnRequestError(UIRequest request, ErrorResponse errorResponse) {
      if (request.ErrorCallback != null || request.OnAfterRun != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (request.OnAfterRun != null)
            request.OnAfterRun();
          if (request.ErrorCallback != null)
            if (errorResponse.IsOperationCanceled()) { // UIRequest are cancelable at any point.

            } else {
              request.ErrorCallback(errorResponse);
            }
        });
      }
    }
  }
}
