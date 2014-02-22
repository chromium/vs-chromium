// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromiumCore.Ipc;
using VsChromiumCore.Ipc.TypedMessages;

namespace VsChromiumPackage.ServerProxy {
  /// <summary>
  /// Component responsible for creating the VsChromium server process and
  /// sending/receiving <see cref="TypedRequest"/> messages. Calling <see
  /// cref="IDisposable.Dispose()"/> ensures the VsChromium server process is
  /// terminated deterministically.
  /// </summary>
  public interface ITypedRequestProcessProxy : IDisposable {
    /// <summary>
    /// Posts a request to be sent to the VsChromium server process, and calls
    /// "successCallback" when the corresponding response is received. Responses
    /// are guaranteed to be called in the same order as the requests are
    /// posted. RunAsync can be called on any thread. "successCallback" will be
    /// called on an unspecified thread.
    /// </summary>
    void RunAsync(TypedRequest request, Action<TypedResponse> successCallback, Action<ErrorResponse> errorCallback);
    /// <summary>
    /// Event raised when the server proxy receives an event from the the
    /// VsChromium server. The event is fired on an unspecified thread.
    /// </summary>
    event Action<TypedEvent> EventReceived;
  }
}
