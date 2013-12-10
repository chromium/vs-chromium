// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Ipc;

namespace VsChromiumPackage.Threads {
  public interface IRequestQueue : IDisposable {
    /// <summary>
    /// Enqueue a request. Non blocking.
    /// </summary>
    /// <param name="request"></param>
    void Enqueue(IpcRequest request);

    /// <summary>
    /// Return the first request from the queue. Blocks until a request is available in the queue.
    /// </summary>
    IpcRequest Dequeue();
  }
}
