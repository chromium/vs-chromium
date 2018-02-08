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
          if (request.OnThreadPoolSend != null)
            Logger.WrapActionInvocation(request.OnThreadPoolSend);

          _typedRequestProcessProxy.RunAsync(request.Request,
            response => OnRequestSuccess(request, response),
            errorResponse => OnRequestError(request, errorResponse));
        },
      };

      _delayedOperationExecutor.Post(operation);
    }

    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;

    public bool IsServerRunning => _typedRequestProcessProxy.IsServerRunning;

    private void TypedRequestProcessProxyOnProcessStarted(object sender, EventArgs args) {
      _synchronizationContextProvider.UIContext.Post(() => OnProcessStarted());
    }

    private void TypedRequestProcessProxyOnProcessFatalError(object sender, ErrorEventArgs args) {
      _synchronizationContextProvider.UIContext.Post(() => OnProcessFatalError(args));
    }

    private void OnRequestSuccess(DispatchThreadServerRequest request, TypedResponse response) {
      if (request.OnThreadPoolReceive != null)
        Logger.WrapActionInvocation(request.OnThreadPoolReceive);

      if (request.OnDispatchThreadSuccess != null) {
        _synchronizationContextProvider.UIContext.Post(() =>
          request.OnDispatchThreadSuccess(response));
      }
    }

    private void OnRequestError(DispatchThreadServerRequest request, ErrorResponse errorResponse) {
      if (request.OnThreadPoolReceive != null)
        Logger.WrapActionInvocation(request.OnThreadPoolReceive);

      if (request.OnDispatchThreadError != null) {
        _synchronizationContextProvider.UIContext.Post(() => {
          if (errorResponse.IsOperationCanceled()) {
            // UIRequest are cancelable at any point.
          } else {
            request.OnDispatchThreadError(errorResponse);
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
