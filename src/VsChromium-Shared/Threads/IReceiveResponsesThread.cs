// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using VsChromium.Core.Ipc;

namespace VsChromium.Threads {
  /// <summary>
  /// Abstraction of a thread receiving responses from an instance of IIpcStream.
  /// </summary>
  public interface IReceiveResponsesThread {
    void Start(IIpcStream ipcStream);

    event Action<IpcResponse> ResponseReceived;
    event Action<IpcEvent> EventReceived;
    event EventHandler<ErrorEventArgs> FatalError;
  }
}
