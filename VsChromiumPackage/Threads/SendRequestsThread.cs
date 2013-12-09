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
      this._requestQeueue = requestQueue;
    }

    public void Start(IIpcStream ipcStream) {
      this._ipcStream = ipcStream;
      new Thread(Run) { IsBackground = true }.Start();
    }

    public void WaitOne() {
      this._waitHandle.WaitOne();
    }

    public void Run() {
      try {
        Loop();
      }
      finally {
        this._waitHandle.Set();
      }
    }

    private void Loop() {
      try {
        while (true) {
          var request = this._requestQeueue.Dequeue();
          if (request == null) {
            Logger.Log("No more requests to send. Time to terminate thread.");
            break;
          }
          this._ipcStream.WriteRequest(request);
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in SendRequestsThread.");
        throw;
      }
    }
  }
}
