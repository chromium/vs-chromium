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
    private uint paramCount = 0;
    private object contextData = null;

    public delegate void FunctionTraceEnterDelegate(DkmStackWalkFrame frame, uint[] parameters, object context, out bool suppressExitBreakpoint);
    public delegate void FunctionTraceExitDelegate(DkmStackWalkFrame frame, uint[] parameters, object context);
    public delegate bool TraceExitConditionDelegate(DkmStackWalkFrame frame);

    private class FunctionTraceEntryDataItem : DkmDataItem {
      public uint[] EntryParameters { get; set; }
    }

    public FunctionTracer(DkmNativeInstructionAddress address, uint prologueLength, uint paramCount, object context) {
      this.paramCount = paramCount;
      this.contextData = context;

      DkmProcess process = address.ModuleInstance.Process;
      entryAddress = (DkmNativeInstructionAddress)process.CreateNativeInstructionAddress(
          address.CPUInstructionPart.InstructionPointer + prologueLength);
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
      uint[] parameters = frame.VscxAnalyzeFunctionParams(paramCount);

      bool suppressExitBreakpoint = false;
      if (OnFunctionEntered != null)
        OnFunctionEntered(frame, parameters, contextData, out suppressExitBreakpoint);

      if (!suppressExitBreakpoint) {
        ulong ret = frame.VscxGetReturnAddress();

        DkmInstructionAddress retAddr = thread.Process.CreateNativeInstructionAddress(ret);
        DkmRuntimeInstructionBreakpoint exitBp = DkmRuntimeInstructionBreakpoint.Create(
            Guids.Source.FunctionTraceExit, thread, retAddr, false, null);

        exitBp.SetDataItem(DkmDataCreationDisposition.CreateAlways,
            new FunctionTraceEntryDataItem { EntryParameters = parameters });
        exitBp.SetDataItem(DkmDataCreationDisposition.CreateAlways,
            new FunctionTraceDataItem { Tracer = this });
        exitBp.Enable();
      }
    }

    void IFunctionTracer.OnExitBreakpointHit(DkmRuntimeBreakpoint bp, DkmThread thread, bool hasException) {
      FunctionTraceEntryDataItem traceDataItem = bp.GetDataItem<FunctionTraceEntryDataItem>();

      if (OnFunctionExited != null) {
        uint[] entryParams = (traceDataItem == null) ? null : traceDataItem.EntryParameters;
        OnFunctionExited(thread.GetTopStackWalkFrame(bp.RuntimeInstance), entryParams, contextData);
      }

      // Since this was a one-shot breakpoint, it is unconditionally closed.
      bp.Close();
    }
  }
}
