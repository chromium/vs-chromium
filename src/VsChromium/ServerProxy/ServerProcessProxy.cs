// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.ProtoBuf;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Core.Processes;
using VsChromium.Threads;

namespace VsChromium.ServerProxy {
  [Export(typeof(IServerProcessProxy))]
  public class ServerProcessProxy : IServerProcessProxy {
    private readonly CallbackDictionary _callbacks = new CallbackDictionary();
    private readonly IServerProcessLauncher _serverProcessLauncher;
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
      IServerProcessLauncher serverProcessLauncher,
      IProtoBufSerializer serializer,
      IReceiveResponsesThread receiveResponsesThread,
      IRequestQueue requestQueue,
      ISendRequestsThread sendRequestsThread) {
      _serializer = serializer;
      _receiveResponsesThread = receiveResponsesThread;
      _requestQueue = requestQueue;
      _sendRequestsThread = sendRequestsThread;
      _serverProcessLauncher = serverProcessLauncher;
    }

    public void RunAsync(IpcRequest request, Action<IpcResponse> callback) {
      CreateServerProcess();

      // Order is important below to avoid race conditions!
      _callbacks.Add(request, callback);
      _requestQueue.Enqueue(request);
    }

    public event Action<IpcEvent> EventReceived;

    public void Dispose() {
      if (_tcpListener != null) {
        _tcpListener.Stop();
      }
      if (_tcpClient != null) {
        _tcpClient.Close();
      }
      if (_serverProcessLauncher != null) {
        _serverProcessLauncher.Dispose();
      }
    }

    private void OnEventReceived(IpcEvent obj) {
      // Special case: progress report events are too noisy...
      if (!(obj.Data is ProgressReportEvent)) {
        Logger.LogInfo("Event {0} of type \"{1}\" received from server.", obj.RequestId, obj.Data.GetType().Name);
      }
      var handler = EventReceived;
      if (handler != null)
        handler(obj);
    }

    private void CreateServerProcess() {
        _serverProcessLauncher.CreateProxy(PreCreateProxy, AfterProxyCreated);
    }

    /// <summary>
    /// Note: Calls are serialized by _serverProcessLauncher, so no need to lock
    /// </summary>
    private IEnumerable<string> PreCreateProxy() {
      Logger.LogInfo("PreCreateProxy");

      if (_tcpListener == null) {
        _tcpListener = CreateServerSocket();
      }

      return new string[] {
        ((IPEndPoint) _tcpListener.LocalEndpoint).Port.ToString()
      };
    }

    /// <summary>
    /// Note: Calls are serialized by _serverProcessLauncher, so no need to lock
    /// </summary>
    private void AfterProxyCreated(CreateProcessResult serverProcess) {
      Logger.LogInfo("AfterProxyCreated (pid={0}", serverProcess.Process.Id);
#if PROFILE_SERVER
      var timeout = TimeSpan.FromSeconds(120.0);
      System.Diagnostics.Trace.WriteLine(string.Format("You have {0:n0} seconds to start the server process with a port argument of {1}.", timeout.TotalSeconds, ((IPEndPoint)_tcpListener.LocalEndpoint).Port));
#else
      var timeout = TimeSpan.FromSeconds(5.0);
#endif
      Logger.LogInfo("AfterProxyCreated: Wait for TCP client connection from server process.");
      if (!_waitForConnection.WaitOne(timeout)) {
        throw new InvalidOperationException(
          string.Format("Child process did not connect to server within {0:n0} seconds.", timeout.TotalSeconds));
      }

      _ipcStream = new IpcStreamOverNetworkStream(_serializer, _tcpClient.GetStream());

      // Ensure process is alive and ready to process requests
      Logger.LogInfo("AfterProxyCreated: Wait for \"Hello\" message from server process.");
      WaitForProcessHelloMessage();

      // Start reading process output
      Logger.LogInfo("AfterProxyCreated: Start receive response thread.");
      _receiveResponsesThread.ResponseReceived += response => {
        var callback = _callbacks.Remove(response.RequestId);
        callback(response);
      };
      _receiveResponsesThread.EventReceived += @event => { OnEventReceived(@event); };
      _receiveResponsesThread.Start(_ipcStream);

      Logger.LogInfo("AfterProxyCreated: Start send request thread..");
      _sendRequestsThread.RequestError += OnRequestError;
      _sendRequestsThread.Start(_ipcStream, _requestQueue);
    }

    private void OnRequestError(IpcRequest request, Exception error) {
      var callback = _callbacks.Remove(request.RequestId);
      var response = ErrorResponseHelper.CreateIpcErrorResponse(request, error);
      callback(response);
    }

    private TcpListener CreateServerSocket() {
      Logger.LogInfo("Opening TCP server socket for server process client connection.");
#if PROFILE_SERVER
      int port = 63300;
#else
      int port = 0;
#endif
      var endPoint = new IPEndPoint(IPAddress.Loopback, port);
      var server = new TcpListener(endPoint);
      server.Start();
      Logger.LogInfo("TCP server started on port {0}.", ((IPEndPoint)server.LocalEndpoint).Port);
      server.BeginAcceptTcpClient(ClientConnected, server);
      return server;
    }

    private void ClientConnected(IAsyncResult result) {
      Logger.LogInfo("TCP Server received client connection.");
      _tcpClient = _tcpListener.EndAcceptTcpClient(result);
      _waitForConnection.Set();
    }

    private void WaitForProcessHelloMessage() {
      var response = _ipcStream.ReadResponse();
      if (response == null) {
        Logger.LogInfo("EOF reached on server process standard output (process terminated!)");
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
