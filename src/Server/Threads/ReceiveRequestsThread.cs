// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;
using VsChromium.Server.Ipc;

namespace VsChromium.Server.Threads {
  [Export(typeof(IReceiveRequestsThread))]
  public class ReceiveRequestsThread : IReceiveRequestsThread {
    private readonly IIpcRequestDispatcher _ipcRequestDispatcher;
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public ReceiveRequestsThread(IIpcRequestDispatcher ipcRequestDispatcher) {
      _ipcRequestDispatcher = ipcRequestDispatcher;
    }

    public void Start(IIpcStream ipcStream) {
      _ipcStream = ipcStream;
      new Thread(Run) {IsBackground = true}.Start();
    }

    public void WaitOne() {
      _waitHandle.WaitOne();
    }

    private void Run() {
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
          var request = _ipcStream.ReadRequest();
          if (request == null) {
            Logger.Log("IPC stream has closed. Time to terminate server.");
            break;
          }
          _ipcRequestDispatcher.ProcessRequestAsync(request);
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in ReceiveRequestsThread.");
        throw;
      }
    }
  }
}
