// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc;

namespace VsChromium.Threads {
  /// <summary>
  /// Abstraction of a thread sending responses to an instance of IIpcStream.
  /// </summary>
  public interface ISendRequestsThread {
    void Start(IIpcStream ipcStream, IRequestQueue requestQueue);

    /// <summary>
    /// Fire when there is an error sending a request to the server.
    /// </summary>
    event Action<IpcRequest, Exception> SendRequestError;
  }
}
