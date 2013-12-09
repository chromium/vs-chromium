// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore.Ipc;

namespace VsChromiumServer.Ipc {
  [Export(typeof(IIpcResponseQueue))]
  public class IpcResponseQueue : IIpcResponseQueue {
    private readonly object _lock = new object();
    private readonly Queue<IpcResponse> _responses = new Queue<IpcResponse>();
    private readonly EventWaitHandle _waitHandle = new AutoResetEvent(false);

    public void Enqueue(IpcResponse response) {
      lock (this._lock) {
        this._responses.Enqueue(response);
      }
      this._waitHandle.Set();
    }

    public IpcResponse Dequeue() {
      while (true) {
        var response = TryDequeue();
        if (response != null)
          return response;

        this._waitHandle.WaitOne();
      }
    }

    private IpcResponse TryDequeue() {
      lock (this._lock) {
        if (this._responses.Count == 0)
          return null;

        return this._responses.Dequeue();
      }
    }
  }
}
