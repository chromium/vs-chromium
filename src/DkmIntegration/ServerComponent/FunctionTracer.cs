// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers;

namespace VsChromium.DkmIntegration.ServerComponent {
  // FunctionTracer notifies listeners when a particular function is entered or exited.  It assumes
  // that the entry address specified in the constructor points to code with a valid stack frame,
  // otherwise the behavior will be undefined.
  //
  // FunctionTracer works by installing a breakpoint after the prologue of the function, and when
  // that breakpoint is hit, installing a one-shot breakpoint at the function's return address
  // (obtained by inspecting the stack).
  class FunctionTracer : IFunctionTracer {
    private DkmRuntimeInstructionBreakpoint entryBp = null;
    private DkmNativeInstructionAddress entryAddress = null;
    private StackFrameAnalyzer frameAnalyzer = null;

    public delegate void FunctionTraceEnterDelegate(
        DkmStackWalkFrame frame,
        StackFrameAnalyzer frameAnalyzer, 
        out bool suppressExitBreakpoint);
    public delegate void FunctionTraceExitDelegate(DkmStackWalkFrame frame, StackFrameAnalyzer frameAnalyzer);

    private class FunctionTraceEntryDataItem : DkmDataItem {
      public object[] EntryArgumentValues { get; set; }
    }

    public FunctionTracer(DkmNativeInstructionAddress address, StackFrameAnalyzer analyzer) {
      this.frameAnalyzer = analyzer;

      DkmProcess process = address.ModuleInstance.Process;
      entryAddress = address;
    }

    public void Enable() {
      DkmNativeModuleInstance module = entryAddress.ModuleInstance;
      FunctionTraceDataItem traceDataItem = new FunctionTraceDataItem { Tracer = this };
      entryBp = DkmRuntimeInstructionBreakpoint.Create(
          Guids.Source.FunctionTraceEnter, null, entryAddress, false, null);
      entryBp.SetDataItem(DkmDataCreationDisposition.CreateAlways, traceDataItem);
      entryBp.Enable();
    }

    public event FunctionTraceEnterDelegate OnFunctionEntered;
    public event FunctionTraceExitDelegate OnFunctionExited;

    void IFunctionTracer.OnEntryBreakpointHit(DkmRuntimeBreakpoint bp, DkmThread thread, bool hasException) {
      // The function was just entered.  Install the exit breakpoint on the calling thread at the
      // return address, and notify any listeners.
      DkmStackWalkFrame frame = thread.GetTopStackWalkFrame(bp.RuntimeInstance);

      bool suppressExitBreakpoint = false;
      if (OnFunctionEntered != null)
        OnFunctionEntered(frame, frameAnalyzer, out suppressExitBreakpoint);

      if (!suppressExitBreakpoint) {
        ulong ret = frame.VscxGetReturnAddress();

        DkmInstructionAddress retAddr = thread.Process.CreateNativeInstructionAddress(ret);
        DkmRuntimeInstructionBreakpoint exitBp = DkmRuntimeInstructionBreakpoint.Create(
            Guids.Source.FunctionTraceExit, thread, retAddr, false, null);
        // Capture the value of every argument now, since when the exit breakpoint gets hit, the
        // target function will have already returned and its frame will be cleaned up.
        exitBp.SetDataItem(DkmDataCreationDisposition.CreateAlways,
            new FunctionTraceEntryDataItem { EntryArgumentValues = frameAnalyzer.GetAllArgumentValues(frame) });
        exitBp.SetDataItem(DkmDataCreationDisposition.CreateAlways,
            new FunctionTraceDataItem { Tracer = this });
        exitBp.Enable();
      }
    }

    void IFunctionTracer.OnExitBreakpointHit(DkmRuntimeBreakpoint bp, DkmThread thread, bool hasException) {
      FunctionTraceEntryDataItem traceDataItem = bp.GetDataItem<FunctionTraceEntryDataItem>();

      if (OnFunctionExited != null) {
        StackFrameAnalyzer exitAnalyzer = 
            (traceDataItem == null) 
                ? null 
                : new CachedFrameAnalyzer(frameAnalyzer.Parameters, traceDataItem.EntryArgumentValues);
        DkmStackWalkFrame frame = thread.GetTopStackWalkFrame(bp.RuntimeInstance);
        OnFunctionExited(frame, exitAnalyzer);
      }

      // Since this was a one-shot breakpoint, it is unconditionally closed.
      bp.Close();
    }
  }
}
