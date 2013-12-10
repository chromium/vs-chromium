// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Server {
  public interface IServerProcessProxy : IDisposable {
    void RunAsync(IpcRequest request, Action<IpcResponse> callback);
    event Action<IpcEvent> EventReceived;
  }
}
