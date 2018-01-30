// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using VsChromium.Core.Ipc;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Component responsible for creating the VsChromium server process and
  /// sending/receiving IPC requests. Calling "Dispose" ensures the VsChromium
  /// server process is terminated determinstically.
  /// </summary>
  public interface IServerProcessProxy : IDisposable {
    /// <summary>
    /// Sends a request to the VsChromium server process, and calls "callback"
    /// when the corresponding response is received. There is no guarantee
    /// responses are received in the same order as requests are sent. RunAsync
    /// can be called on any thread. "callback" will be called on an
    /// unspecified thread.
    /// </summary>
    void RunAsync(IpcRequest request, Action<IpcResponse> callback);

    /// <summary>
    /// Event raised when the server proxy receives an event from the the
    /// VsChromium server. The event is fired on an unspecified thread.
    /// </summary>
    event Action<IpcEvent> EventReceived;

    /// <summary>
    /// Event fired when the server process has been successfully started
    /// and confirmed to be responsive.
    /// </summary>
    event EventHandler ProcessStarted;

    /// <summary>
    /// Event fired when the server has encoutered a fatal error, including
    /// the inability to start-up successfully.
    /// </summary>
    event EventHandler<ErrorEventArgs> ProcessFatalError;
  }
}
