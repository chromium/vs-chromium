// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Logging {
  public class StackTraceException : Exception {
    private readonly string _stackTrace;

    public StackTraceException(string message, string stackTrace, Exception inner) : base(message, inner) {
      _stackTrace = stackTrace;
    }

    public override string StackTrace => _stackTrace;
  }
}