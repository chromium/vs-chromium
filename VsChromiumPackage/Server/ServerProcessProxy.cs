// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;
using VsChromiumCore.Ipc.ProtoBuf;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Processes;
using VsChromiumPackage.Threads;

namespace VsChromiumPackage.Server {
  [Export(typeof(IServerProcessProxy))]
  public class ServerProcessProxy : IServerProcessProxy {
    private readonly CallbackDictionary _callbacks = new CallbackDictionary();
    private readonly IProxyServerCreator _proxyServerCreator;
    private readonly IReceiveResponsesThread _receiveResponsesThread;
    private readonly IRequestQueue _requestQueue;
    private readonly ISendRequestsThread _sendRequestsThread;
    private readonly IProtoBufSerializer _serializer;
    private readonly ManualResetEvent _waitForConnection = new ManualResetEvent(false);
    private IpcStreamOverNetworkStream _ipcStream;
    private TcpClient _tcpClient;
    private TcpListener _tcpListener;

    [ImportingConstructor]
    public ServerProcessProxy(
        IProxyServerCreator proxyServerCreator,
        IProtoBufSerializer serializer,
        IReceiveResponsesThread receiveResponsesThread,
        IRequestQueue requestQueue,
        ISendRequestsThread sendRequestsThread) {
      this._serializer = serializer;
      this._receiveResponsesThread = receiveResponsesThread;
      this._requestQueue = requestQueue;
      this._sendRequestsThread = sendRequestsThread;
      this._proxyServerCreator = proxyServerCreator;
    }

    public void RunAsync(IpcRequest request, Action<IpcResponse> callback) {
      CreateServerProcess();

      // Order is important below to avoid race conditions!
      this._callbacks.Add(request, callback);
      this._requestQueue.Enqueue(request);
    }

    public event Action<IpcEvent> EventReceived;

    public void Dispose() {
      if (this._tcpListener != null) {
        this._tcpListener.Stop();
      }
      if (this._tcpClient != null) {
        this._tcpClient.Close();
      }
      if (this._proxyServerCreator != null) {
        this._proxyServerCreator.Dispose();
      }
    }

    protected virtual void OnEventReceived(IpcEvent obj) {
      // Special case: progress report events are too noisy...
      if (!(obj.Data is ProgressReportEvent)) {
        Logger.Log("Event {0} of type \"{1}\" received from server.", obj.RequestId, obj.Data.GetType().Name);
      }
      var handler = EventReceived;
      if (handler != null)
        handler(obj);
    }

    private void CreateServerProcess() {
      this._proxyServerCreator.CreateProxy(PreCreateProxy, AfterProxyCreated);
    }

    private IEnumerable<string> PreCreateProxy() {
      this._tcpListener = CreateServerSocket();

      return new string[] {
        ((IPEndPoint)this._tcpListener.LocalEndpoint).Port.ToString()
      };
    }

    private void AfterProxyCreated(ProcessProxy serverProcess) {
      var timeout = TimeSpan.FromSeconds(5.0);
#if PROFILE_SERVER
      timeout = TimeSpan.FromSeconds(120.0);
      Trace.WriteLine(string.Format("You have {0:n0} seconds to start the server process with a port argument of {1}.", timeout.TotalSeconds, ((IPEndPoint)_tcpListener.LocalEndpoint).Port));
#endif
      if (!this._waitForConnection.WaitOne(timeout)) {
        throw new InvalidOperationException(
            string.Format("Child process did not connect to server within {0:n0} seconds.", timeout.TotalSeconds));
      }

      this._ipcStream = new IpcStreamOverNetworkStream(this._serializer, this._tcpClient.GetStream());

      // Ensure process is alive and ready to process requests
      WaitForProcessHelloMessage();

      // Start reading process output
      this._receiveResponsesThread.ResponseReceived += response => {
        var callback = this._callbacks.Remove(response.RequestId);
        callback(response);
      };
      this._receiveResponsesThread.EventReceived += @event => { OnEventReceived(@event); };
      this._receiveResponsesThread.Start(this._ipcStream);
      this._sendRequestsThread.Start(this._ipcStream);
    }

    private TcpListener CreateServerSocket() {
      var server = new TcpListener(IPAddress.Loopback, 0);
      server.Start();
      Logger.Log("TCP server started on port {0}", ((IPEndPoint)server.LocalEndpoint).Port);
      server.BeginAcceptTcpClient(ClientConnected, server);
      return server;
    }

    private void ClientConnected(IAsyncResult result) {
      Logger.Log("TCP Server received client connection.");
      this._tcpClient = this._tcpListener.EndAcceptTcpClient(result);
      this._waitForConnection.Set();
    }

    private void WaitForProcessHelloMessage() {
      var response = this._ipcStream.ReadResponse();
      if (response == null) {
        Logger.Log("EOF reached on server process standard output (process terminated!)");
        throw new InvalidOperationException("EOF reached on server process standard output (process terminated!)");
      }

      if (response.Data == null ||
          response.Data.GetType() != HelloWorldProtocol.Response.Data.GetType() ||
          (response.Data as IpcStringData).Text != (HelloWorldProtocol.Response.Data as IpcStringData).Text) {
        throw new InvalidOperationException("Server process did not send correct hello world message!");
      }
    }
  }
}
