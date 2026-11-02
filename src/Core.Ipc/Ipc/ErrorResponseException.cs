// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Ipc {
  public class ErrorResponseException : Exception {
    private readonly ErrorResponse _errorResponse;

    public ErrorResponseException(ErrorResponse errorResponse)
      : base(errorResponse.Message) {
      _errorResponse = errorResponse;
    }

    public ErrorResponse ErrorResponse { get { return _errorResponse; } }
  }
}