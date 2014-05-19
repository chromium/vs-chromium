// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32.Processes;
using VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers;

namespace VsChromium.DkmIntegration.ServerComponent {
  class AutoAttachToChildHandler : DkmDataItem {
    private List<FunctionTracer> _functionTracers;

    public AutoAttachToChildHandler() {
      _functionTracers = new List<FunctionTracer>();
    }

    public void OnModuleInstanceLoad(DkmNativeModuleInstance module, DkmWorkList workList) {
      bool isKernel32 = module.Name.Equals("Kernel32.dll", StringComparison.CurrentCultureIgnoreCase);
      bool isAdvapi = module.Name.Equals("Advapi32.dll", StringComparison.CurrentCultureIgnoreCase);
      if (isKernel32 || isAdvapi) {
        DkmSystemInformationFlags systemInformationFlags = module.Process.SystemInformation.Flags;
        bool isTarget64Bit = systemInformationFlags.HasFlag(DkmSystemInformationFlags.Is64Bit);
        int pointerSize = (isTarget64Bit) ? 8 : 4;
        FunctionParameter[] createProcessParams = {
            new FunctionParameter("lpApplicationName", pointerSize, pointerSize),
            new FunctionParameter("lpCommandLine", pointerSize, pointerSize),
            new FunctionParameter("lpProcessAttributes", pointerSize, pointerSize),
            new FunctionParameter("lpThreadAttributes", pointerSize, pointerSize),
            new FunctionParameter("bInheritHandles", 4, pointerSize),
            new FunctionParameter("dwCreationFlags", 4, pointerSize),
            new FunctionParameter("lpEnvironment", pointerSize, pointerSize),
            new FunctionParameter("lpCurrentDirectory", pointerSize, pointerSize),
            new FunctionParameter("lpStartupInfo", pointerSize, pointerSize),
            new FunctionParameter("lpProcessInformation", pointerSize, pointerSize)
        };
        FunctionParameter[] createProcessAsUserParams = {
            new FunctionParameter("hToken", pointerSize, pointerSize)
        };
        createProcessAsUserParams = createProcessAsUserParams.Concat(createProcessParams).ToArray();
        StackFrameAnalyzer createProcessFrameAnalyzer = null;
        StackFrameAnalyzer createProcessAsUserFrameAnalyzer = null;

        if (isTarget64Bit) {
          createProcessFrameAnalyzer = new X64FrameAnalyzer(createProcessParams);
          createProcessAsUserFrameAnalyzer = new X64FrameAnalyzer(createProcessAsUserParams);
        } else {
          createProcessFrameAnalyzer = new StdcallFrameAnalyzer(createProcessParams);
          createProcessAsUserFrameAnalyzer = new StdcallFrameAnalyzer(createProcessAsUserParams);
        }

        // Advapi32 doesn't export CreateProcessW, only CreateProcessAsUserW.
        if (isKernel32)
          HookCreateProcess(module, "CreateProcessW", createProcessFrameAnalyzer);

        // Both Advapi32 and Kernel32 export CreateProcessAsUserW.
        HookCreateProcess(module, "CreateProcessAsUserW", createProcessAsUserFrameAnalyzer);
      }
    }

    private void HookCreateProcess(DkmNativeModuleInstance module, string export, StackFrameAnalyzer frameAnalyzer) {
      try {
        FunctionTracer tracer = new FunctionTracer(
            module.FindExportName(export, true), frameAnalyzer);
        tracer.OnFunctionEntered += createProcessTracer_OnFunctionEntered;
        tracer.OnFunctionExited += createProcessTracer_OnFunctionExited;
        tracer.Enable();

        _functionTracers.Add(tracer);
      } catch (DkmException) {
        // For some reason, sandboxed processes act strangely (e.g. FindExportName throws an
        // exception with E_FAIL.  It's not clear why this happens, but these processes can't
        // create child processes anyway, so just handle this failure gracefully.
        return;
      }
    }

    void createProcessTracer_OnFunctionEntered(
        DkmStackWalkFrame frame, 
        StackFrameAnalyzer functionAnalyzer, 
        out bool suppressExitBreakpoint) {
      // If this was not created with CREATE_SUSPENDED, then we can't automatically attach to this
      // child process.
      // TODO(zturner): OR in CREATE_SUSPENDED using WriteProcessMemory, then when the exit bp
      // hits, check if we OR'ed in CREATE_SUSPENDED, and if so, resume the process after the
      // attach.
      ProcessCreationFlags flags = (ProcessCreationFlags)
          Convert.ToUInt32(functionAnalyzer.GetArgumentValue(frame, "dwCreationFlags"));
      suppressExitBreakpoint = !flags.HasFlag(ProcessCreationFlags.CREATE_SUSPENDED);
    }

    void createProcessTracer_OnFunctionExited(
        DkmStackWalkFrame frame, 
        StackFrameAnalyzer frameAnalyzer) {
      ulong processInfoAddr = Convert.ToUInt64(
          frameAnalyzer.GetArgumentValue(frame, "lpProcessInformation"));

      // Check the return address first, it should be in EAX.  CreateProcessAsUser and
      // CreateProcess both return 0 on failure.  If the function failed, there is no child to
      // attach to.
      if (0 == frame.VscxGetRegisterValue32(CpuRegister.Eax))
        return;

      // The process was successfully created.  Extract the PID from the PROCESS_INFORMATION
      // output param.  An attachment request must happend through the EnvDTE, which can only
      // be accessed from the VsPackage, so a request must be sent via a component message.
      DkmProcess process = frame.Process;
      int size = Marshal.SizeOf(typeof(PROCESS_INFORMATION));
      byte[] buffer = new byte[size];
      process.ReadMemory(processInfoAddr, DkmReadMemoryFlags.None, buffer);
      PROCESS_INFORMATION info = MarshalUtility.ByteArrayToStructure<PROCESS_INFORMATION>(buffer);
      DkmCustomMessage attachRequest = DkmCustomMessage.Create(
          process.Connection,
          process,
          Guids.Source.VsPackageMessage,
          (int)Messages.Component.MessageCode.AttachToChild,
          process.LivePart.Id,
          info.dwProcessId);
      attachRequest.SendToVsService(PackageServices.DkmComponentEventHandler, false);
    }
  }
}
