// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Threads;

namespace VsChromium.ServerProxy {
  [Export(typeof(IServerProcessProxy))]
  public class ServerProcessProxy : IServerProcessProxy {
    private readonly CallbackDictionary _callbacks = new CallbackDictionary();
    private readonly IServerProcessLauncher _serverProcessLauncher;
    private readonly IReceiveResponsesThread _receiveResponsesThread;
    private readonly IRequestQueue _requestQueue;
    private readonly ISendRequestsThread _sendRequestsThread;
    private readonly Lazy<Task> _createProcessTask;
    private IIpcStream _ipcStream;
    private bool _isServerRunning;

    [ImportingConstructor]
    public ServerProcessProxy(
      IServerProcessLauncher serverProcessLauncher,
      IReceiveResponsesThread receiveResponsesThread,
      IRequestQueue requestQueue,
      ISendRequestsThread sendRequestsThread) {
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
      _serverProcessLauncher?.Dispose();
    }

    /// <summary>
    /// Return existing or create new process task
    /// </summary>
    /// <returns></returns>
    private Task CreateServerProcessAsync() {
      // The task is intentionally shared (created once via Lazy<T>) and runs on the thread pool
      // (TaskScheduler.Default), so it cannot deadlock the UI thread.
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
      return _createProcessTask.Value;
#pragma warning restore VSTHRD003
    }

    private Task CreateProcessLazyWorker() {
      var task = _serverProcessLauncher.StartServerAsync();
      return task.ContinueWith(t => AfterProcessStarted(t.Result),
        // Make sure to run on thread pool even if called from a UI thread
        TaskScheduler.Default);
    }

    private void AfterProcessStarted(ServerProcess serverProcess) {
      Invariants.Assert(serverProcess != null);

      Logger.LogInfo("AfterProcessStarted (pid={0})", serverProcess.Process.Id);
      _ipcStream = serverProcess.Stream;

      // Ensure process is alive and ready to process requests
      Logger.LogInfo("AfterProcessStarted: Wait for \"Hello\" message from server process.");
      WaitForProcessHelloMessage();

      // Start reading process output
      Logger.LogInfo("AfterProcessStarted: Start receive response thread.");
      _receiveResponsesThread.ResponseReceived += response => {
        var callback = _callbacks.Remove(response.RequestId);
        callback(response);
      };
      _receiveResponsesThread.EventReceived += @event => { OnEventReceived(@event); };
      _receiveResponsesThread.FatalError += (sender, args) => { HandleReceiveThreadFatalError(args); };
      _receiveResponsesThread.Start(_ipcStream);

      Logger.LogInfo("AfterProcessStarted: Start send request thread..");
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
