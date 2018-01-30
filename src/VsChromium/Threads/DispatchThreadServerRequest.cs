// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Threads {
  public class DispatchThreadServerRequest {
    public DispatchThreadServerRequest() {
      Delay = TimeSpan.FromSeconds(0.1);
    }

    /// <summary>
    /// (Required) The request ID, used to cancel previous request with the same
    /// ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// (Optional) Timespan to wait before starting the request (default is 0.1
    /// second)
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// (Required) The request object to send to the server.
    /// </summary>
    public TypedRequest Request { get; set; }

    /// <summary>
    /// (Optional) Action executed on a background thread when the request delay
    /// has expired, just before the request is sent to the server.
    /// </summary>
    public Action OnSend { get; set; }

    /// <summary>
    /// (Optional) Action executed on a brackground thread when a response
    /// (either sucess, error or cancellation) is received from the server, just
    /// before calling <see cref="OnSuccess"/> or <see cref="OnError"/>.
    /// </summary>
    public Action OnReceive { get; set; }

    /// <summary>
    /// (Optional) Action executed on the UI thread once the server request has
    /// been successfully executed.
    /// </summary>
    public Action<TypedResponse> OnSuccess { get; set; }

    /// <summary>
    /// (Optional) Action executed on the UI thread if request processing
    /// resulted in an error.
    /// </summary>
    public Action<ErrorResponse> OnError { get; set; }
  }
}
