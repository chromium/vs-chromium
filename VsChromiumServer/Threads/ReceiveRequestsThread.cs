// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;
using VsChromiumServer.Ipc;

namespace VsChromiumServer.Threads {
  [Export(typeof(IReceiveRequestsThread))]
  public class ReceiveRequestsThread : IReceiveRequestsThread {
    private readonly IIpcRequestDispatcher _ipcRequestDispatcher;
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public ReceiveRequestsThread(IIpcRequestDispatcher ipcRequestDispatcher) {
      this._ipcRequestDispatcher = ipcRequestDispatcher;
    }

    public void Start(IIpcStream ipcStream) {
      this._ipcStream = ipcStream;
      new Thread(Run) { IsBackground = true }.Start();
    }

    public void WaitOne() {
      this._waitHandle.WaitOne();
    }

    private void Run() {
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
          var request = this._ipcStream.ReadRequest();
          if (request == null) {
            Logger.Log("EOF reached on stdin. Time to terminate server.");
            break;
          }
          this._ipcRequestDispatcher.ProcessRequestAsync(request);
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in ReceiveRequestsThread.");
        throw;
      }
    }
  }
}
