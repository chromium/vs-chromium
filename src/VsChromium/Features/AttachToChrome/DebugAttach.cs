// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Processes;
using VsChromium.DkmIntegration;

namespace VsChromium.Features.AttachToChrome {
  static class DebugAttach {
    public static void AttachToProcess(Process[] processes, bool autoAttachToChildren) {
      List<VsDebugTargetInfo2> targetList = new List<VsDebugTargetInfo2>();
      IntPtr targetsBuffer = IntPtr.Zero;
      int targetSize = Marshal.SizeOf(typeof(VsDebugTargetInfo2));
      int guidSize = Marshal.SizeOf(typeof(Guid));

      try {
        foreach (Process process in processes) {
          NtProcess ntproc = new NtProcess(process.Id);

          VsDebugTargetInfo2 target = new VsDebugTargetInfo2();
          DebugProcessOptions options = new DebugProcessOptions { AutoAttachToChildren = autoAttachToChildren };
          target.dwDebugEngineCount = 1;
          target.dwProcessId = (uint)process.Id;
          target.dlo = (uint)DEBUG_LAUNCH_OPERATION.DLO_AlreadyRunning;
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
        Core.Logger.Log("Launching {0} debug targets", processes.Length);
        int hr = debugger.LaunchDebugTargets2((uint)processes.Length, targetsBuffer);
        if (hr != 0) {
          IVsUIShell shell = (IVsUIShell)VsPackage.GetGlobalService(typeof(SVsUIShell));
          string error;
          shell.GetErrorInfo(out error);
          Core.Logger.LogError("An error occured while attaching to process (hr = 0x{0:x}).  {1}", hr, error);
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
