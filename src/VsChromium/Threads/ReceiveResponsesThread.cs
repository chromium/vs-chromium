// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;

namespace VsChromium.Threads {
  [Export(typeof(IReceiveResponsesThread))]
  public class ReceiveResponsesThread : IReceiveResponsesThread {
    private IIpcStream _ipcStream;

    [ImportingConstructor]
    public ReceiveResponsesThread() {
    }

    public void Start(IIpcStream ipcStream) {
      _ipcStream = ipcStream;
      new Thread(Run) { IsBackground = true }.Start();
    }

    public event Action<IpcResponse> ResponseReceived;
    public event Action<IpcEvent> EventReceived;
    public event EventHandler<ErrorEventArgs> FatalError;

    private void Run() {
      Logger.LogInfo("ReceiveRequestsThread: Starting thread");
      Loop();
      Logger.LogInfo("ReceiveRequestsThread: Terminating thread");
    }

    private void Loop() {
      try {
        while (true) {
          var response = _ipcStream.ReadResponse();
          if (response == null) {
            Logger.LogInfo("ReceiveRequestsThread: EOF reached on ipc stream, exiting thread loop");
            break;
          }

          var @event = response as IpcEvent;
          if (@event != null) {
            OnEventReceived(@event);
          } else {
            OnResponseReceived(response);
          }
        }
      } catch (Exception e) {
        Logger.LogError(e, "ReceiveRequestsThread: unexpected exception, exting thread loop");
        OnFatalError(new ErrorEventArgs(e));
      }
    }

    protected virtual void OnResponseReceived(IpcResponse obj) {
      ResponseReceived?.Invoke(obj);
    }

    protected virtual void OnEventReceived(IpcEvent obj) {
      EventReceived?.Invoke(obj);
    }

    protected virtual void OnFatalError(ErrorEventArgs e) {
      FatalError?.Invoke(this, e);
    }
  }
}
