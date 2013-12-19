// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;

namespace VsChromiumPackage.ServerProxy {
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
    }

    public void RunAsync(TypedRequest request, Action<TypedResponse> callback) {
      var sw = Stopwatch.StartNew();

      var ipcRequest = new IpcRequest {
        RequestId = _ipcRequestIdFactory.GetNextId(),
        Protocol = IpcProtocols.TypedMessage,
        Data = request
      };

      // Note: We capture the value outside the RunAsync callback.
      var localSequenceNumber = Interlocked.Increment(ref _currentSequenceNumber);

      _serverProcessProxy.RunAsync(ipcRequest, ipcResponse => {
        lock (_lock) {
          _bufferedResponses.Add(new BufferedResponse {
            SequenceNumber = localSequenceNumber,
            IpcRequest = ipcRequest,
            IpcResponse = ipcResponse,
            Callback = callback,
            Elapsed = sw.Elapsed
          });
        }
        OnResponseReceived();
      });
    }

    public event Action<TypedEvent> EventReceived;

    public void Dispose() {
      _serverProcessProxy.Dispose();
    }

    private void ServerProcessProxyOnEventReceived(IpcEvent ipcEvent) {
      var @event = ipcEvent.Data as TypedEvent;
      if (@event != null)
        OnEventReceived(@event);
    }

    protected virtual void OnEventReceived(TypedEvent obj) {
      var handler = EventReceived;
      if (handler != null)
        handler(obj);
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

    private static void SendResponses(IEnumerable<BufferedResponse> reponsesToSend) {
      reponsesToSend.ForAll(bufferedResponse => {
        Logger.Log("Request {0} of type \"{1}\" took {2:n0} msec to handle.",
                   bufferedResponse.IpcRequest.RequestId, bufferedResponse.IpcRequest.Data.GetType().Name,
                   bufferedResponse.Elapsed.TotalMilliseconds);

        // TODO(rpaquay): The reponse protocol might be |IpcProtocols.Exception|.
        // Should we handle this case it here?
        if (bufferedResponse.IpcResponse.Protocol == IpcProtocols.TypedMessage) {
          bufferedResponse.Callback((TypedResponse)bufferedResponse.IpcResponse.Data);
        }
      });
    }

    public class BufferedResponse : IComparable<BufferedResponse> {
      public long SequenceNumber { get; set; }
      public IpcRequest IpcRequest { get; set; }
      public IpcResponse IpcResponse { get; set; }
      public Action<TypedResponse> Callback { get; set; }
      public TimeSpan Elapsed { get; set; }

      public int CompareTo(BufferedResponse other) {
        if (other == null)
          return 1;
        return SequenceNumber.CompareTo(other.SequenceNumber);
      }
    }
  }
}
