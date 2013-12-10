// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Threads {
  [Export(typeof(IReceiveResponsesThread))]
  public class ReceiveResponsesThread : IReceiveResponsesThread {
    private readonly EventWaitHandle _waitHandle = new ManualResetEvent(false);
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public ReceiveResponsesThread() {
    }

    public void Start(IIpcStream ipcStream) {
      _ipcStream = ipcStream;
      new Thread(Run) {IsBackground = true}.Start();
    }

    public void WaitOne() {
      _waitHandle.WaitOne();
    }

    public event Action<IpcResponse> ResponseReceived;

    public event Action<IpcEvent> EventReceived;

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
          var response = _ipcStream.ReadResponse();
          if (response == null) {
            Logger.Log("EOF reached on stdin. Time to terminate server.");
            break;
          }

          var @event = response as IpcEvent;
          if (@event != null) {
            OnEventReceived(@event);
          } else {
            OnResponseReceived(response);
          }
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Exception in ReceiveRequestsThread.");
        throw;
      }
    }

    protected virtual void OnResponseReceived(IpcResponse obj) {
      var handler = ResponseReceived;
      if (handler != null)
        handler(obj);
    }

    protected virtual void OnEventReceived(IpcEvent obj) {
      var handler = EventReceived;
      if (handler != null)
        handler(obj);
    }
  }
}
