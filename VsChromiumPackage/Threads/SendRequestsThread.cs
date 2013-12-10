// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Threads {
  [Export(typeof(ISendRequestsThread))]
  public class SendRequestsThread : ISendRequestsThread {
    private readonly IRequestQueue _requestQeueue;
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public SendRequestsThread(IRequestQueue requestQueue) {
      _requestQeueue = requestQueue;
    }

    public void Start(IIpcStream ipcStream) {
      _ipcStream = ipcStream;
      new Thread(Run) {IsBackground = true}.Start();
    }

    public void WaitOne() {
      _waitHandle.WaitOne();
    }

    public void Run() {
      try {
        Loop();
      }
      finally {
        _waitHandle.Set();
      }
    }

    private void Loop() {
      try {
        while (true) {
          var request = _requestQeueue.Dequeue();
          if (request == null) {
            Logger.Log("No more requests to send. Time to terminate thread.");
            break;
          }
          _ipcStream.WriteRequest(request);
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in SendRequestsThread.");
        throw;
      }
    }
  }
}
