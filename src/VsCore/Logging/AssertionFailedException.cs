// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Logging {
  public class AssertionFailedException : Exception {
    private readonly string _stackTrace;

    public AssertionFailedException(string message) : base(message) {
    }

    public AssertionFailedException(string message, string stackTrace) : base(message) {
      _stackTrace = stackTrace;
    }

    public override string StackTrace => _stackTrace ?? base.StackTrace;
  }
}