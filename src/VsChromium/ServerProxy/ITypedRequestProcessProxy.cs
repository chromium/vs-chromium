// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.ServerProxy {
  /// <summary>
  /// Component responsible for creating the VsChromium server process and
  /// sending/receiving <see cref="TypedRequest"/> messages. Calling <see
  /// cref="IDisposable.Dispose()"/> ensures the VsChromium server process is
  /// terminated deterministically.
  /// </summary>
  public interface ITypedRequestProcessProxy : IDisposable {
    /// <summary>
    /// Posts a request to be sent to the VsChromium server process, and calls
    /// <paramref name="successCallback"/> when the corresponding response is received,
    /// or <paramref name="errorCallback"/> if an error occurred.
    /// 
    /// <para>Responses are guaranteed to be called in the same order as the requests are
    /// posted. RunAsync can be called on any thread. <paramref name="successCallback"/>
    /// will be called on an unspecified thread.</para>
    /// </summary>
    void RunAsync(TypedRequest request, RunAsyncOptions options, Action<TypedResponse> successCallback, Action<ErrorResponse> errorCallback);

    /// <summary>
    /// Same as <see cref="RunAsync"/>, except responsed are dispatched as soon as they are received,
    /// irrespective of the send order.
    /// </summary>
    void RunUnbufferedAsync(TypedRequest request, RunAsyncOptions options, Action<TypedResponse> successCallback, Action<ErrorResponse> errorCallback);

    /// <summary>
    /// Event raised when the server proxy receives an event from the the
    /// VsChromium server. The event is fired on an unspecified thread.
    /// </summary>
    event Action<TypedEvent> EventReceived;

    /// <summary>
    /// Event raised when the server has started and is ready to process requests
    /// </summary>
    event EventHandler ProcessStarted;

    /// <summary>
    /// Event raise when the server encountered a fatal error and is not properly running anymore
    /// </summary>
    event EventHandler<ErrorEventArgs> ProcessFatalError;
    bool IsServerRunning { get; }
  }

  [Flags]
  public enum RunAsyncOptions {
    Default = 0x00,
    /// <summary>
    /// Ensures the request is processed on the sequential
    /// queue on the server (i.e. all requests with this flag will always be
    /// executed sequentially on the server).
    /// </summary>
    RunOnSequentialQueue = 0x01,
  }
}
