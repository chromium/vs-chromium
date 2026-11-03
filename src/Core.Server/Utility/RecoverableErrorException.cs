// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Utility {
  /// <summary>
  /// An error reported to the client (JSON-RPC error -32001) that is considered recoverable by
  /// the side emitting it. This applies for example to requests with invalid
  /// parameters, which don't break assumption about the internal state of
  /// the side emitting the error.
  /// </summary>
  public class RecoverableErrorException : ApplicationException {
    public RecoverableErrorException(string message)
      : base(message) {
    }
  }
}
