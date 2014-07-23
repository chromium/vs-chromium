// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.DkmShared;
using VsChromium.Core.Logging;
using VsChromium.Core.Processes;

namespace VsChromium.Features.AttachToChrome {
  static class DebugAttach {
    public static void AttachToProcess(Process[] processes, ChildDebuggingMode mode) {
      List<VsDebugTargetInfo2> targetList = new List<VsDebugTargetInfo2>();
      IntPtr targetsBuffer = IntPtr.Zero;
      int targetSize = Marshal.SizeOf(typeof(VsDebugTargetInfo2));
      int guidSize = Marshal.SizeOf(typeof(Guid));

      try {
        foreach (Process process in processes) {
          NtProcess ntproc = new NtProcess(process.Id);
          VsDebugTargetInfo2 target = new VsDebugTargetInfo2();
          DebugProcessOptions options = new DebugProcessOptions { ChildDebuggingMode = mode };
          target.dwDebugEngineCount = 1;
          target.dwProcessId = (uint)process.Id;
          target.dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_AlreadyRunning;
          if (process.Threads.Count == 1) {
            // If this is a suspended process, then using DLO_AttachToSuspendedLaunchProcess will
            // bypass the initial loader breakpoint, causing a seamless and transparent attach.
            // This is usually the desired behavior, as child processes frequently startup and
            // shutdown, and it is intrusive to be constantly breaking into the debugger.
            ProcessThread mainThread = process.Threads[0];
            if (mainThread.ThreadState == ThreadState.Wait && mainThread.WaitReason == ThreadWaitReason.Suspended)
              target.dlo |= (uint)_DEBUG_LAUNCH_OPERATION4.DLO_AttachToSuspendedLaunchProcess;
          }
          target.bstrExe = ntproc.Win32ProcessImagePath;
          target.cbSize = (uint)targetSize;
          target.bstrCurDir = null;
          target.guidPortSupplier = DkmIntegration.Guids.PortSupplier.Default;
          target.LaunchFlags = (uint)(__VSDBGLAUNCHFLAGS.DBGLAUNCH_Silent | __VSDBGLAUNCHFLAGS.DBGLAUNCH_WaitForAttachComplete);
          target.bstrOptions = options.OptionsString;
          target.pDebugEngines = Marshal.AllocCoTaskMem(guidSize);
          Marshal.StructureToPtr(DkmEngineId.NativeEng, target.pDebugEngines, false);
          targetList.Add(target);
        }
        int elementSize = Marshal.SizeOf(typeof(VsDebugTargetInfo2));
        targetsBuffer = Marshal.AllocCoTaskMem(targetList.Count * elementSize);
        for (int i = 0; i < targetList.Count; ++i) {
          IntPtr writeAddr = targetsBuffer + i * elementSize;
          Marshal.StructureToPtr(targetList[i], writeAddr, false);
        }

        IVsDebugger2 debugger = (IVsDebugger2)VsPackage.GetGlobalService(typeof(SVsShellDebugger));
        Logger.Log("Launching {0} debug targets", processes.Length);
        int hr = debugger.LaunchDebugTargets2((uint)processes.Length, targetsBuffer);
        if (hr != 0) {
          IVsUIShell shell = (IVsUIShell)VsPackage.GetGlobalService(typeof(SVsUIShell));
          string error;
          shell.GetErrorInfo(out error);
          Logger.LogError("An error occured while attaching to process (hr = 0x{0:x}).  {1}", hr, error);
        }
      } finally {
        foreach (VsDebugTargetInfo2 target in targetList) {
          if (target.pDebugEngines != IntPtr.Zero)
            Marshal.FreeCoTaskMem(target.pDebugEngines);
        }

        if (targetsBuffer != IntPtr.Zero)
          Marshal.FreeCoTaskMem(targetsBuffer);
      }
    }
  }
}
