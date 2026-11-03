// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Core.Ipc {
  public static class ErrorResponseHelper {
    public static IpcResponse CreateIpcErrorResponse(long requestId, Exception error) {
      return new IpcResponse {
        RequestId = requestId,
        Protocol = IpcProtocols.Exception,
        Data = CreateErrorResponse(error)
      };
    }

    public static ErrorResponse CreateErrorResponse(Exception e) {
      if (e == null)
        return null;

      return new ErrorResponse {
        Message = e.Message,
        StackTrace = e.StackTrace,
        FullTypeName = e.GetType().FullName,
        InnerError = CreateErrorResponse(e.InnerException)
      };
    }
  }
}
