// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Logging;
using VsChromium.ServerProxy;

namespace VsChromium.Threads {
  /// <summary>
  /// Sends requests to the server after a delay (on the thread pool), and
  /// calls the result callbacks on the dispatch (UI) thread.
  /// </summary>
  [Export(typeof(IDispatchThreadServerRequestExecutor))]
  public class DispatchThreadServerRequestExecutor : IDispatchThreadServerRequestExecutor {
    private readonly IServerClient _serverClient;
    private readonly IDelayedOperationExecutor _delayedOperationExecutor;
    private readonly ISynchronizationContextProvider _synchronizationContextProvider;
    private readonly object _lock = new object();
    /// <summary>
    /// The cancellation of the running requests, by request ID.
    /// </summary>
    private readonly Dictionary<string, CancellationTokenSource> _runningRequests =
      new Dictionary<string, CancellationTokenSource>();
    /// <summary>
    /// The last request posted with <see cref="DispatchThreadServerRequest{TResult}.RunSequentially"/>.
    /// </summary>
    private Task _lastSequentialRequest = Task.FromResult(true);

    [ImportingConstructor]
    public DispatchThreadServerRequestExecutor(IServerClient serverClient,
                              IDelayedOperationExecutor delayedOperationExecutor,
                              ISynchronizationContextProvider synchronizationContextProvider) {
      _serverClient = serverClient;
      _delayedOperationExecutor = delayedOperationExecutor;
      _synchronizationContextProvider = synchronizationContextProvider;
      _serverClient.ProcessStarted += ServerClientOnProcessStarted;
      _serverClient.ProcessFatalError += ServerClientOnProcessFatalError;
    }

    public void Post<TResult>(DispatchThreadServerRequest<TResult> request) {
      if (request == null)
        throw new ArgumentNullException(nameof(request));
      if (request.Id == null)
        throw new ArgumentException(@"Request must have an Id.", nameof(request));
      if (request.Request == null)
        throw new ArgumentException(@"Request must have a request function.", nameof(request));

      var operation = new DelayedOperation {
        Id = request.Id,
        Delay = request.Delay,
        // Action executed on a background thread when delay has expired.
        Action = () => SendRequest(request),
      };

      _delayedOperationExecutor.Post(operation);
    }

    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;

    public bool IsServerRunning => _serverClient.IsServerRunning;

    private void SendRequest<TResult>(DispatchThreadServerRequest<TResult> request) {
      if (request.OnThreadPoolSend != null)
        Logger.WrapActionInvocation(request.OnThreadPoolSend);

      var cancellationTokenSource = new CancellationTokenSource();
      lock (_lock) {
        // The request supersedes the running request with the same ID.
        CancellationTokenSource previous;
        if (_runningRequests.TryGetValue(request.Id, out previous)) {
          previous.Cancel();
        }
        _runningRequests[request.Id] = cancellationTokenSource;

        if (request.RunSequentially) {
          _lastSequentialRequest = _lastSequentialRequest
            .ContinueWith(_ => RunRequestAsync(request, cancellationTokenSource),
              CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default)
            .Unwrap();
          return;
        }
      }

      // Note: RunRequestAsync never fails (errors are reported to the callbacks).
      _ = Task.Run(() => RunRequestAsync(request, cancellationTokenSource));
    }

    private async Task RunRequestAsync<TResult>(DispatchThreadServerRequest<TResult> request,
      CancellationTokenSource cancellationTokenSource) {
      var result = default(TResult);
      ServerError error = null;
      try {
        cancellationTokenSource.Token.ThrowIfCancellationRequested();
        var client = await _serverClient.GetProtocolClientAsync().ConfigureAwait(false);
        result = await request.Request(client, cancellationTokenSource.Token).ConfigureAwait(false);
      }
      catch (Exception e) {
        error = ServerError.FromException(e);
        if (error.IsOperationCanceled) {
          // Requests are cancelable at any point.
        } else if (error.IsRecoverable) {
          Logger.LogInfo("Server request \"{0}\" failed: {1}", request.Id, error.Message);
        } else {
          Logger.LogError(e, "Error executing server request \"{0}\".", request.Id);
        }
      }
      finally {
        lock (_lock) {
          CancellationTokenSource current;
          if (_runningRequests.TryGetValue(request.Id, out current) && current == cancellationTokenSource) {
            _runningRequests.Remove(request.Id);
          }
        }
      }

      if (request.OnThreadPoolReceive != null)
        Logger.WrapActionInvocation(request.OnThreadPoolReceive);

      if (error == null) {
        if (request.OnDispatchThreadSuccess != null) {
          _synchronizationContextProvider.DispatchThreadContext.Post(() =>
            request.OnDispatchThreadSuccess(result));
        }
      } else if (!error.IsOperationCanceled) {
        if (request.OnDispatchThreadError != null) {
          _synchronizationContextProvider.DispatchThreadContext.Post(() =>
            request.OnDispatchThreadError(error));
        }
      }
    }

    private void ServerClientOnProcessStarted(object sender, EventArgs args) {
      _synchronizationContextProvider.DispatchThreadContext.Post(() => OnProcessStarted());
    }

    private void ServerClientOnProcessFatalError(object sender, ErrorEventArgs args) {
      _synchronizationContextProvider.DispatchThreadContext.Post(() => OnProcessFatalError(args));
    }

    protected virtual void OnProcessStarted() {
      ProcessStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnProcessFatalError(ErrorEventArgs e) {
      ProcessFatalError?.Invoke(this, e);
    }
  }
}
