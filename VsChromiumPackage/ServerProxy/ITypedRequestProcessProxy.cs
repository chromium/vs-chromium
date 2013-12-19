// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumPackage.ServerProxy {
  /// <summary>
  /// Component responsible for creating the VsChromium server process and
  /// sending/receiving Typed requests. Calling "Dispose" ensures the VsChromium
  /// server process is terminated determinstically.
  /// </summary>
  public interface ITypedRequestProcessProxy : IDisposable {
    /// <summary>
    /// Sends a request to the VsChromium server process, and calls "callback" when
    /// the corresponding response is received. The responses are guaranteed to
    /// be called in the same order as the requests arrived.
    /// RunAsync can be called on any thread. "callback" will be called on an
    /// undetermined thread.
    /// </summary>
    void RunAsync(TypedRequest request, Action<TypedResponse> callback);
    /// <summary>
    /// Event raised when the server proxy receives an event from the the VsChromium server.
    /// The event is fired on an undermined thread.
    /// </summary>
    event Action<TypedEvent> EventReceived;
  }
}
