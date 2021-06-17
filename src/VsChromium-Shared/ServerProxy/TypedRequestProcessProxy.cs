﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.ServerProxy {
  [Export(typeof(ITypedRequestProcessProxy))]
  public class TypedRequestProcessProxy : ITypedRequestProcessProxy {
    private readonly SortedSet<BufferedResponse> _bufferedResponses = new SortedSet<BufferedResponse>();
    private readonly IIpcRequestIdFactory _ipcRequestIdFactory;
    private readonly object _lock = new object();
    private readonly IServerProcessProxy _serverProcessProxy;
    private long _currentSequenceNumber;
    private long _nextExpectedSequenceNumber = 1; // _currentSequenceNumber + 1

    [ImportingConstructor]
    public TypedRequestProcessProxy(IServerProcessProxy serverProcessProxy, IIpcRequestIdFactory ipcRequestIdFactory) {
      _serverProcessProxy = serverProcessProxy;
      _ipcRequestIdFactory = ipcRequestIdFactory;
      _serverProcessProxy.EventReceived += ServerProcessProxyOnEventReceived;
      _serverProcessProxy.ProcessStarted += ServerProcessProxyOnProcessStarted;
      _serverProcessProxy.ProcessFatalError += ServerProcessProxyOnProcessFatalError;
    }

    public event Action<TypedEvent> EventReceived;
    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;

    public bool IsServerRunning => _serverProcessProxy.IsServerRunning;

    public void RunAsync(TypedRequest request, RunAsyncOptions options, Action<TypedResponse> successCallback, Action<ErrorResponse> errorCallback) {
      // Note: We capture the value outside the RunAsync callback.
      var localSequenceNumber = Interlocked.Increment(ref _currentSequenceNumber);

      RunAsyncWorker(request, options, successCallback, errorCallback, localSequenceNumber, response => {
        lock (_lock) {
          _bufferedResponses.Add(response);
        }
        OnResponseReceived();
      });
    }

    public void RunUnbufferedAsync(TypedRequest request, RunAsyncOptions options, Action<TypedResponse> successCallback, Action<ErrorResponse> errorCallback) {
      RunAsyncWorker(request, options, successCallback, errorCallback, -1, SendResponse);
    }

    public void RunAsyncWorker(TypedRequest request, RunAsyncOptions options, Action<TypedResponse> successCallback,
      Action<ErrorResponse> errorCallback, long sequenceNumber, Action<BufferedResponse> processResponse) {
      var sw = Stopwatch.StartNew();

      var ipcRequest = new IpcRequest {
        RequestId = _ipcRequestIdFactory.GetNextId(),
        Protocol = IpcProtocols.TypedMessage,
        RunOnSequentialQueue = (options & RunAsyncOptions.RunOnSequentialQueue) != 0,
        Data = request
      };

      _serverProcessProxy.RunAsync(ipcRequest, ipcResponse => {
        var response = new BufferedResponse {
          SequenceNumber = sequenceNumber,
          IpcRequest = ipcRequest,
          IpcResponse = ipcResponse,
          SuccessCallback = successCallback,
          ErrorCallback = errorCallback,
          Elapsed = sw.Elapsed
        };
        processResponse(response);
      });
    }

    public void Dispose() {
      _serverProcessProxy.Dispose();
    }

    private void ServerProcessProxyOnEventReceived(IpcEvent ipcEvent) {
      var @event = ipcEvent.Data as TypedEvent;
      if (@event != null)
        OnEventReceived(@event);
    }

    private void OnResponseReceived() {
      var reponsesToSend = new List<BufferedResponse>();
      lock (_lock) {
        foreach (var entry in _bufferedResponses) {
          if (entry.SequenceNumber != _nextExpectedSequenceNumber)
            break;

          reponsesToSend.Add(entry);
          _nextExpectedSequenceNumber++;
        }

        foreach (var entry in reponsesToSend) {
          _bufferedResponses.Remove(entry);
        }
      }

      SendResponses(reponsesToSend);
    }

    private void ServerProcessProxyOnProcessStarted(object sender, EventArgs args) {
      OnProcessStarted();
    }

    private void ServerProcessProxyOnProcessFatalError(object sender, ErrorEventArgs args) {
      OnProcessFatalError(args);
    }

    private static void SendResponses(IEnumerable<BufferedResponse> reponsesToSend) {
      reponsesToSend.ForAll(SendResponse);
    }

    private static void SendResponse(BufferedResponse bufferedResponse) {
      Logger.LogInfo("Server request #{0} ({1}) took {2:n0} msec to execute.",
        bufferedResponse.IpcRequest.RequestId,
        GetRequestDescription(bufferedResponse.IpcRequest),
        bufferedResponse.Elapsed.TotalMilliseconds);

      if (bufferedResponse.IpcResponse.Protocol == IpcProtocols.TypedMessage) {
        bufferedResponse.SuccessCallback((TypedResponse) bufferedResponse.IpcResponse.Data);
      } else if (bufferedResponse.IpcResponse.Protocol == IpcProtocols.Exception) {
        bufferedResponse.ErrorCallback((ErrorResponse) bufferedResponse.IpcResponse.Data);
      } else {
        var error = new InvalidOperationException(string.Format("Unknown response protocol: {0}",
          bufferedResponse.IpcResponse.Protocol));
        var errorResponse = ErrorResponseHelper.CreateErrorResponse(error);
        bufferedResponse.ErrorCallback(errorResponse);
      }
    }

    private static string GetRequestDescription(IpcRequest request) {
      try {
        return request.ToString();
      }
      catch (Exception) {
        return request.Data.GetType().Name;
      }
    }

    public class BufferedResponse : IComparable<BufferedResponse> {
      public long SequenceNumber { get; set; }
      public IpcRequest IpcRequest { get; set; }
      public IpcResponse IpcResponse { get; set; }
      public Action<TypedResponse> SuccessCallback { get; set; }
      public Action<ErrorResponse> ErrorCallback { get; set; }
      public TimeSpan Elapsed { get; set; }

      public int CompareTo(BufferedResponse other) {
        if (other == null)
          return 1;
        return SequenceNumber.CompareTo(other.SequenceNumber);
      }
    }

    protected virtual void OnEventReceived(TypedEvent obj) {
      EventReceived?.Invoke(obj);
    }

    protected virtual void OnProcessStarted() {
      ProcessStarted?.Invoke(this, EventArgs.Empty);
    }

    protected virtual void OnProcessFatalError(ErrorEventArgs e) {
      ProcessFatalError?.Invoke(this, e);
    }
  }
}
