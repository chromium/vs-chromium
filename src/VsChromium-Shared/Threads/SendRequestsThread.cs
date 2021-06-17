﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;

namespace VsChromium.Threads {
  [Export(typeof(ISendRequestsThread))]
  public class SendRequestsThread : ISendRequestsThread {
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IRequestQueue _requestQeueue;
    private IIpcStream _ipcStream;

    public void Start(IIpcStream ipcStream, IRequestQueue requestQueue) {
      _ipcStream = ipcStream;
      _requestQeueue = requestQueue;
      new Thread(Run) { IsBackground = true }.Start();
    }

    public event Action<IpcRequest, Exception> SendRequestError;

    protected virtual void OnSendRequestError(IpcRequest request, Exception error) {
      SendRequestError?.Invoke(request, error);
    }

    private void Run() {
      try {
        Logger.LogInfo("Starting SendRequests thread.");
        Loop();
      } finally {
        _waitHandle.Set();
      }
    }

    private void Loop() {
      try {
        while (true) {
          var request = _requestQeueue.Dequeue();
          if (request == null) {
            Logger.LogInfo("No more requests to send. Time to terminate thread.");
            break;
          }
          try {
            SendRequest(request);
          } catch (Exception e) {
            OnSendRequestError(request, e);
          }
        }
      } catch (Exception e) {
        Logger.LogError(e, "Exception in SendRequestsThread.");
        throw;
      }
    }

    private void SendRequest(IpcRequest request) {
      try {
        _ipcStream.WriteRequest(request);
      } catch (Exception e) {
        throw new IpcRequestException(request, e);
      }
    }
  }
}