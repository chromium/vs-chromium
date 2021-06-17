﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
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
    private readonly Lazy<Task> _createProcessTask;
    private readonly ManualResetEvent _waitForConnection = new ManualResetEvent(false);
    private IpcStreamOverNetworkStream _ipcStream;
    private TcpClient _tcpClient;
    private TcpListener _tcpListener;
    private bool _isServerRunning;

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
      _createProcessTask = new Lazy<Task>(CreateProcessLazyWorker, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public event Action<IpcEvent> EventReceived;
    public event EventHandler ProcessStarted;
    public event EventHandler<ErrorEventArgs> ProcessFatalError;

    public bool IsServerRunning => _isServerRunning;

    public void RunAsync(IpcRequest request, Action<IpcResponse> callback) {
      CreateServerProcessAsync().ContinueWith(t => {
          // Register callback so that we can reply with error if needed
          _callbacks.Add(request, callback);

          if (t.Exception != null) {
            // Skip the "AggregateException"
            var error = t.Exception.InnerExceptions.Count == 1 ? t.Exception.InnerExceptions[0] : t.Exception;

            // Reply error to callback (this will also fire general "server is down" event)
            HandleSendRequestError(request, error);
          } else {
            // The queue is guaranteed to be started at this point, so enqueue the request
            // so it is sent to the server
            _requestQueue.Enqueue(request);
          }
        },
        new CancellationToken(),
        TaskContinuationOptions.ExecuteSynchronously,
        // Make sure to run on thread pool even if called from a UI thread
        TaskScheduler.Default);
    }

    public void Dispose() {
      _tcpListener?.Stop();
      _tcpClient?.Close();
      _serverProcessLauncher?.Dispose();
    }

    /// <summary>
    /// Return existing or create new process task
    /// </summary>
    /// <returns></returns>
    private Task CreateServerProcessAsync() {
      return _createProcessTask.Value;
    }

    private Task CreateProcessLazyWorker() {
      var task = _serverProcessLauncher.CreateProxyAsync(PreCreateProxy());
      return task.ContinueWith(t => AfterProxyCreated(t.Result),
        // Make sure to run on thread pool even if called from a UI thread
        TaskScheduler.Default);
    }

    private IList<string> PreCreateProxy() {
      //
      // Note: Calls are serialized by _serverProcessLauncher, so no need to lock
      //
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

    private void AfterProxyCreated(CreateProcessResult processResult) {
      Invariants.Assert(processResult != null);

      Logger.LogInfo("AfterProxyCreated (pid={0}", processResult.Process.Id);
#if PROFILE_SERVER
      var timeout = TimeSpan.FromSeconds(120.0);
      System.Diagnostics.Trace.WriteLine(string.Format(
        "You have {0:n0} seconds to start the server process with a port argument of {1}.", timeout.TotalSeconds,
        ((IPEndPoint) _tcpListener.LocalEndpoint).Port));
#else
      var timeout = TimeSpan.FromSeconds(30.0);
#endif
      Logger.LogInfo("AfterProxyCreated: Wait for TCP client connection from server process.");
      var serverStartedSuccessfully = _waitForConnection.WaitOne(timeout) && _tcpClient != null;
      Invariants.CheckOperation(serverStartedSuccessfully, $"Child process did not connect to server within {timeout.TotalSeconds:n0} seconds.");

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
      _receiveResponsesThread.FatalError += (sender, args) => { HandleReceiveThreadFatalError(args); };
      _receiveResponsesThread.Start(_ipcStream);

      Logger.LogInfo("AfterProxyCreated: Start send request thread..");
      _sendRequestsThread.SendRequestError += HandleSendRequestError;
      _sendRequestsThread.Start(_ipcStream, _requestQueue);

      // Server is fully started, notify consumers
      _isServerRunning = true;
      OnProcessStarted();
    }

    private void HandleReceiveThreadFatalError(ErrorEventArgs args) {
      // Terminate all pending requests with errors
      foreach (var kvp in _callbacks.RemoveAll()) {
        var response = ErrorResponseHelper.CreateIpcErrorResponse(kvp.Key, args.GetException());
        kvp.Value(response);
      }
      // We assume the server is down as soon as there is an error
      // sending a request.
      _isServerRunning = false;
      OnProcessFatalError(args);
    }

    private void HandleSendRequestError(IpcRequest request, Exception error) {
      var callback = _callbacks.Remove(request.RequestId);
      var response = ErrorResponseHelper.CreateIpcErrorResponse(request.RequestId, error);
      callback(response);

      // We assume the server is down as soon as there is an error
      // sending a request.
      _isServerRunning = false;
      OnProcessFatalError(new ErrorEventArgs(error));
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

    protected void OnEventReceived(IpcEvent obj) {
      // Special case: progress report events are too noisy...
      if (!(obj.Data is ProgressReportEvent)) {
        Logger.LogInfo("Event {0} of type \"{1}\" received from server.", obj.RequestId, obj.Data.GetType().Name);
      }
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
