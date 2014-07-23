// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;

namespace VsChromium.Threads {
  [Export(typeof(IRequestQueue))]
  public class RequestQueue : IRequestQueue {
    private readonly object _lock = new object();
    private readonly Queue<IpcRequest> _requests = new Queue<IpcRequest>();
    private readonly EventWaitHandle _waitHandle = new AutoResetEvent(false);
    private bool _disposed;

    public void Enqueue(IpcRequest request) {
      lock (_lock) {
        _requests.Enqueue(request);
      }
      _waitHandle.Set();
    }

    public IpcRequest Dequeue() {
      while (true) {
        var request = TryDequeue();
        if (request != null)
          return request;

        _waitHandle.WaitOne();
        if (_disposed)
          return null;
      }
    }

    private IpcRequest TryDequeue() {
      lock (_lock) {
        if (_requests.Count == 0)
          return null;

        return _requests.Dequeue();
      }
    }

    public void Dispose() {
      Logger.Log("Disposing RequestQueue.");
      _disposed = true;
      _waitHandle.Set();
    }
  }
}
