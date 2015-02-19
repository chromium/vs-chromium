// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.Operations {
  public class OperationHandlers {
    public Action<OperationInfo> OnBeforeExecute { get; set; }
    public Action<OperationInfo> Execute { get; set; }
    public Action<OperationInfo, Exception> OnError { get; set; }
  }
}