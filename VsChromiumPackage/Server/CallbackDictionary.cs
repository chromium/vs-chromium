// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Server {
  public class CallbackDictionary {
    private readonly Dictionary<long, Action<IpcResponse>> _callbacks = new Dictionary<long, Action<IpcResponse>>();
    private readonly Object _lock = new object();

    public void Add(IpcRequest request, Action<IpcResponse> callback) {
      lock (this._lock) {
        this._callbacks.Add(request.RequestId, callback);
      }
    }

    public Action<IpcResponse> Remove(long requestId) {
      lock (this._lock) {
        var result = this._callbacks[requestId];
        this._callbacks.Remove(requestId);
        return result;
      }
    }
  }
}
