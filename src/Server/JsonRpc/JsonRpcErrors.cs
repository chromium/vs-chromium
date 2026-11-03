// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Protocol.Generated;

namespace VsChromium.Server.JsonRpc {
  public static class JsonRpcErrors {
    /// <summary>
    /// The <see cref="ErrorInfo"/> of an exception (and of its inner exceptions), null if
    /// <paramref name="error"/> is null.
    /// </summary>
    public static ErrorInfo CreateErrorInfo(Exception error) {
      if (error == null) {
        return null;
      }
      return new ErrorInfo {
        Message = error.Message,
        TypeName = error.GetType().FullName,
        StackTrace = error.StackTrace,
        Inner = CreateErrorInfo(error.InnerException),
      };
    }
  }
}
