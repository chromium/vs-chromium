// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;

namespace VsChromium.DkmIntegration.ServerComponent {
  class RuntimeBreakpointHandler : DkmDataItem {
    public void OnRuntimeBreakpointReceived(
        DkmRuntimeBreakpoint bp,
        DkmThread thread,
        bool hasException,
        DkmEventDescriptorS eventDescriptor) {
      FunctionTraceDataItem traceDataItem = bp.GetDataItem<FunctionTraceDataItem>();
      if (traceDataItem != null && traceDataItem.Tracer != null) {
        if (bp.SourceId == Guids.Source.FunctionTraceEnter)
          traceDataItem.Tracer.OnEntryBreakpointHit(bp, thread, hasException);
        else if (bp.SourceId == Guids.Source.FunctionTraceExit)
          traceDataItem.Tracer.OnExitBreakpointHit(bp, thread, hasException);
      }
    }
  }
}
