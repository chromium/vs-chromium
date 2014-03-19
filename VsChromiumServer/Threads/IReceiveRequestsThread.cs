// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc;

namespace VsChromium.Server.Threads {
  /// <summary>
  /// Abstraction of a thread receiving requests from an instance of IIpcStream.
  /// </summary>
  public interface IReceiveRequestsThread {
    void Start(IIpcStream ipcStream);
    void WaitOne();
  }
}
