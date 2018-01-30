// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.ServerProxy;

namespace VsChromium.Threads {
  [Export(typeof(IDispatchThreadServerRequestExecutor))]
  public class DispatchThreadServerRequestExecutor : IDispatchThreadServerRequestExecutor {
    private readonly ITypedRequestProcessProxy _typedRequestProcessProxy;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;

    [ImportingConstructor]
    public DispatchThreadServerRequestExecutor(ITypedRequestProcessProxy typedRequestProcessProxy,
                              IDelayedOperationExecutor delayedOperationExecutor,
                              ISynchronizationContextProvider synchronizationContextProvider) {
      _typedRequestProcessProxy = typedRequestProcessProxy;
      _delayedOperationExecutor = delayedOperationExecutor;
      _synchronizationContextProvider = synchronizationContextProvider;
      _typedRequestProcessProxy.ProcessStarted += TypedRequestProcessProxyOnProcessStarted;
      _typedRequestProcessProxy.ProcessFatalError += TypedRequestProcessProxyOnProcessFatalError;
    }

    public void Post(DispatchThreadServerRequest request) {
      if (request == null)
        throw new ArgumentNullException("request");
      if (request.Id == null)
        throw new ArgumentException(@"Request must have an Id.", "request");
      if (request.Request == null)
        throw new ArgumentException(@"Request must have a typed request.", "request");

      var operation = new DelayedOperation {
        Id = request.Id,
        Delay = request.Delay,
        // Action executed on a background thread when delay has expired.
        Action = () => {
          if (request.OnSend != null)
            Logger.WrapActionInvocation(request.OnSend);

          _typedRequestProcessProxy.RunAsync(request.Request,
            response => OnRequestSuccess(request, response),
            errorResponse => OnRequestError(request, errorResponse));
        },
      };

      _delayedOperationExecutor.Post(operation);
    }

    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;

    private void TypedRequestProcessProxyOnProcessStarted(object sender, EventArgs args) {
      _synchronizationContextProvider.UIContext.Post(() => OnProcessStarted());
    }

    private void TypedRequestProcessProxyOnProcessFatalError(object sender, ErrorEventArgs args) {
      _synchronizationContextProvider.UIContext.Post(() => OnProcessFatalError(args));
    }

    private void OnRequestSuccess(DispatchThreadServerRequest request, TypedResponse response) {
      if (request.OnReceive != null)
        Logger.WrapActionInvocation(request.OnReceive);

      if (request.OnSuccess != null) {
        _synchronizationContextProvider.UIContext.Post(() =>
          request.OnSuccess(response));
      }
    }

    private void OnRequestError(DispatchThreadServerRequest request, ErrorResponse errorResponse) {
      if (request.OnReceive != null)
        Logger.WrapActionInvocation(request.OnReceive);

      if (request.OnError != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (errorResponse.IsOperationCanceled()) {
            // UIRequest are cancelable at any point.
          } else {
            request.OnError(errorResponse);
          }
        });
      }
    }

    protected virtual void OnProcessStarted() {
      ProcessStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnProcessFatalError(ErrorEventArgs e) {
      ProcessFatalError?.Invoke(this, e);
    }
  }
}
