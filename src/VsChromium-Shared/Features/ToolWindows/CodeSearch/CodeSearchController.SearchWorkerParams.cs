// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public partial class CodeSearchController {
    private class SearchWorkerParams {
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
      /// The request to sent to the server
      /// </summary>
      public TypedRequest TypedRequest { get; set; }
      /// <summary>
      /// Amount of time to wait before sending the request to the server.
      /// </summary>
      public TimeSpan Delay { get; set; }
      /// <summary>
      /// Lambda invoked when the response to the request has been successfully
      /// received from the server.
      /// </summary>
      public Action<TypedResponse, Stopwatch> ProcessResponse { get; set; }
      /// <summary>
      /// Lambda invoked when the request resulted in an error from the server.
      /// </summary>
      public Action<ErrorResponse, Stopwatch> ProcessError { get; set; }
    }
  }
}