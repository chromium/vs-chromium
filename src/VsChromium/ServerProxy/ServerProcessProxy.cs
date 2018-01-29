// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
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
    /// <summary>
    /// If the server did not start successfully, store the exception here so it can
    /// be thrown to the caller.
    /// </summary>
    private Exception _serverFatalException;

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
      CreateServerProcessAsync();

      // Any request after a server failure results in a failed response to the caller
      if (_serverFatalException != null) {
        Task.Run(() => { OnRequestError(request, _serverFatalException); });
        return;
      }

      // Order is important below to avoid race conditions!
      _callbacks.Add(request, callback);
      _requestQueue.Enqueue(request);
    }

    public event Action<IpcEvent> EventReceived;

    public void Dispose() {
      _tcpListener?.Stop();
      _tcpClient?.Close();
      _serverProcessLauncher?.Dispose();
    }

    private void OnEventReceived(IpcEvent obj) {
      // Special case: progress report events are too noisy...
      if (!(obj.Data is ProgressReportEvent)) {
        Logger.LogInfo("Event {0} of type \"{1}\" received from server.", obj.RequestId, obj.Data.GetType().Name);
      }
      EventReceived?.Invoke(obj);
    }

    private void CreateServerProcessAsync() {
      _serverProcessLauncher.CreateProxyAsync(PreCreateProxy, AfterProxyCreated);
    }

    /// <summary>
    /// Note: Calls are serialized by _serverProcessLauncher, so no need to lock
    /// </summary>
    private IList<string> PreCreateProxy() {
      Logger.LogInfo("PreCreateProxy");

      // Create a listener socket, waiting for the server process to connect
      if (_tcpListener == null) {
        _tcpListener = CreateServerSocket();
      }

      // Return the port of the listening socket
      return new[] {
        ((IPEndPoint) _tcpListener.LocalEndpoint).Port.ToString()
      };
    }

    /// <summary>
    /// Note: Calls are serialized by _serverProcessLauncher, so no need to lock
    /// </summary>
    private void AfterProxyCreated(Exception exception, CreateProcessResult processResult) {
      // If we could not create the server process, remember that fact
      // and reply to all pending request with an error. New requests will
      // also immediately get notified with an error.
      if (processResult == null) {
        _serverFatalException = exception;
        ReplyToPendingRequestsWithServerError();
        return;
      }

      // The server proxy process started, but the server itself need to connect
      // back to use to open the communication socket. This may not happen in
      // error cases. Create a background task to wait for the server, and
      // start processing request 
      var task = Task.Run(() => WaitForServerConnectionTask(processResult));
      task.ContinueWith(t => {
        if (t.Exception != null) {
          _serverFatalException = t.Exception;
          ReplyToPendingRequestsWithServerError();
        }
      }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    private void WaitForServerConnectionTask(CreateProcessResult processResult) {
      Invariants.Assert(processResult != null);

      Logger.LogInfo("AfterProxyCreated (pid={0}", processResult.Process.Id);
#if PROFILE_SERVER
      var timeout = TimeSpan.FromSeconds(120.0);
      System.Diagnostics.Trace.WriteLine(string.Format(
        "You have {0:n0} seconds to start the server process with a port argument of {1}.", timeout.TotalSeconds,
        ((IPEndPoint) _tcpListener.LocalEndpoint).Port));
#else
      var timeout = TimeSpan.FromSeconds(5.0);
#endif
      Logger.LogInfo("AfterProxyCreated: Wait for TCP client connection from server process.");
      if (!_waitForConnection.WaitOne(timeout) || _tcpClient == null) {
        throw new InvalidOperationException(
          $"Child process did not connect to server within {timeout.TotalSeconds:n0} seconds.");
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

    private void ReplyToPendingRequestsWithServerError() {
      foreach (var x in _callbacks.RemoveAll()) {
        Task.Run(() => SendErrorToCallback(x.Key, _serverFatalException, x.Value));
      }
    }

    private void OnRequestError(IpcRequest request, Exception error) {
      var callback = _callbacks.Remove(request.RequestId);
      SendErrorToCallback(request.RequestId, error, callback);
    }

    private static void SendErrorToCallback(long requestId, Exception error, Action<IpcResponse> callback) {
      var response = ErrorResponseHelper.CreateIpcErrorResponse(requestId, error);
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
      try {
        _tcpClient = _tcpListener.EndAcceptTcpClient(result);
      } catch (ObjectDisposedException e) {
        Logger.LogWarn(e, "Error accepting connection from server: socket has been disposed.");
      } catch (Exception e) {
        Logger.LogError(e, "Error acceping connection from server process.");
      }

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
