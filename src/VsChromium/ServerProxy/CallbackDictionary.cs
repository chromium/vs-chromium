// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc;

namespace VsChromium.ServerProxy {
  public class CallbackDictionary {
    private readonly Dictionary<long, Action<IpcResponse>> _callbacks = new Dictionary<long, Action<IpcResponse>>();
    private readonly object _lock = new object();

    public void Add(IpcRequest request, Action<IpcResponse> callback) {
      lock (_lock) {
        _callbacks.Add(request.RequestId, callback);
      }
    }

    public Action<IpcResponse> Remove(long requestId) {
      lock (_lock) {
        var result = _callbacks[requestId];
        _callbacks.Remove(requestId);
        return result;
      }
    }
  }
}
