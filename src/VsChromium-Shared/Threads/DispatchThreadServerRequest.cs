// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

namespace VsChromium.Threads {
  public class DispatchThreadServerRequest<TResult> {
    public DispatchThreadServerRequest() {
      Delay = TimeSpan.FromSeconds(0.1);
    }

    /// <summary>
    /// (Required) The request ID: a request supersedes the previous request
    /// with the same ID (cancelled if it is not finished yet).
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// (Optional) Timespan to wait before starting the request (default is 0.1
    /// second)
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// (Required) Sends the request to the server, e.g.
    /// <c>(client, token) =&gt; client.SearchCodeAsync(parameters, token)</c>.
    /// The token is cancelled when the request is superseded.
    /// </summary>
    public Func<ProtocolClient, CancellationToken, Task<TResult>> Request { get; set; }

    /// <summary>
    /// (Optional) Sends the request only when the previous requests with this
    /// flag are finished (i.e. these requests are sent and executed in the
    /// order they were posted), e.g. for file registrations.
    /// </summary>
    public bool RunSequentially { get; set; }

    /// <summary>
    /// (Optional) Action executed on a background thread when the request delay
    /// has expired, just before the request is sent to the server.
    /// </summary>
    public Action OnThreadPoolSend { get; set; }

    /// <summary>
    /// (Optional) Action executed on a brackground thread when a response
    /// (either sucess, error or cancellation) is received from the server, just
    /// before calling <see cref="OnDispatchThreadSuccess"/> or <see cref="OnDispatchThreadError"/>.
    /// </summary>
    public Action OnThreadPoolReceive { get; set; }

    /// <summary>
    /// (Optional) Action executed on the UI thread once the server request has
    /// been successfully executed.
    /// </summary>
    public Action<TResult> OnDispatchThreadSuccess { get; set; }

    /// <summary>
    /// (Optional) Action executed on the UI thread if request processing
    /// resulted in an error (not called if the request was cancelled).
    /// </summary>
    public Action<ServerError> OnDispatchThreadError { get; set; }
  }
}
