// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;
using VsChromiumServer.Ipc;

namespace VsChromiumServer.Threads {
  [Export(typeof(ISendResponsesThread))]
  public class SendResponsesThread : ISendResponsesThread {
    private readonly IIpcResponseQueue _ipcResponseQeueue;
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public SendResponsesThread(IIpcResponseQueue ipcResponseQueue) {
      _ipcResponseQeueue = ipcResponseQueue;
    }

    public void Start(IIpcStream ipcStream) {
      _ipcStream = ipcStream;
      new Thread(Run) {IsBackground = true}.Start();
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
          var response = _ipcResponseQeueue.Dequeue();
          Debug.Assert(response != null);
          _ipcStream.WriteResponse(response);
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in SendResponseThread.");
        throw;
      }
    }
  }
}
