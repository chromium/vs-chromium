// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;

namespace VsChromium.DkmIntegration.ServerComponent {
  class FunctionTraceDataItem : DkmDataItem {
    public IFunctionTracer Tracer { get; set; }
  }
}
