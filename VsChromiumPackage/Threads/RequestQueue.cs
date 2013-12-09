// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromiumCore;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Threads {
  [Export(typeof(IRequestQueue))]
  public class RequestQueue : IRequestQueue {
    private readonly object _lock = new object();
    private readonly Queue<IpcRequest> _requests = new Queue<IpcRequest>();
    private readonly EventWaitHandle _waitHandle = new AutoResetEvent(false);
    private bool _disposed;

    public void Enqueue(IpcRequest request) {
      lock (this._lock) {
        this._requests.Enqueue(request);
      }
      this._waitHandle.Set();
    }

    public IpcRequest Dequeue() {
      while (true) {
        var response = TryDequeue();
        if (response != null)
          return response;

        this._waitHandle.WaitOne();
        if (this._disposed)
          return null;
      }
    }

    private IpcRequest TryDequeue() {
      lock (this._lock) {
        if (this._requests.Count == 0)
          return null;

        return this._requests.Dequeue();
      }
    }

    public void Dispose() {
      Logger.Log("Disposing RequestQueue.");
      this._disposed = true;
      this._waitHandle.Set();
    }
  }
}
