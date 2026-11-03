// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public partial class CodeSearchController {
    private class SearchWorkerParams<TResult> {
      /// <summary>
      /// Simple short name of the operation (for debugging only).
      /// </summary>
      public string OperationName { get; set; }
      /// <summary>
      /// Short description of the operation (for display in status bar
      /// progress)
      /// </summary>
      public string HintText { get; set; }
      /// <summary>
      /// Sends the request to the server.
      /// </summary>
      public Func<ProtocolClient, CancellationToken, Task<TResult>> Request { get; set; }
      /// <summary>
      /// Amount of time to wait before sending the request to the server.
      /// </summary>
      public TimeSpan Delay { get; set; }
      /// <summary>
      /// Lambda invoked when the response to the request has been successfully
      /// received from the server.
      /// </summary>
      public Action<TResult, Stopwatch> ProcessResponse { get; set; }
      /// <summary>
      /// Lambda invoked when the request resulted in an error from the server.
      /// </summary>
      public Action<ServerError, Stopwatch> ProcessError { get; set; }
    }
  }
}
