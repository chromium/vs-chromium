// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
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

namespace VsChromium.DkmIntegration.ServerComponent {
  class AutoAttachToChildHandler : DkmDataItem {
    private class CreateProcessDataItem : DkmDataItem {
      private uint processInformationAddr;

      public const uint kFlagsIndex = 5;
      public const uint kProcessInformationIndex = 9;
      public const uint kArgumentCount = 10;

      public CreateProcessDataItem(DkmStackWalkFrame frame) {
        // The pointer value which refers to the PROCESS_INFORMATION structure.
        processInformationAddr = frame.VscxGetArgumentValue(kProcessInformationIndex);
      }

      public CreateProcessDataItem(uint[] arguments) {
        processInformationAddr = arguments[kProcessInformationIndex];
      }

      // Pointer to the lpProcessInformation argument, so that it can be analyzed after the
      // function returns.
      public ulong ProcessInformationAddr { get { return processInformationAddr; } }
    }

    private class CreateProcessContextData {
      public int FlagsParamIndex { get; set; }
      public int ProcessInformationParamIndex { get; set; }
    }

    private FunctionTracer createProcessTracer;
    private FunctionTracer createProcessAsUserTracer;

    public AutoAttachToChildHandler() {
    }

    public void OnModuleInstanceLoad(DkmNativeModuleInstance module, DkmWorkList workList) {
      if (module.Name.Equals("KernelBase.dll", StringComparison.CurrentCultureIgnoreCase)) {
        createProcessTracer = new FunctionTracer(
            module.FindExportName("CreateProcessW", true),
            5,
            10,
            new CreateProcessContextData { FlagsParamIndex = 5, ProcessInformationParamIndex = 9 });

        createProcessAsUserTracer = new FunctionTracer(
            module.FindExportName("CreateProcessAsUserW", true),
            5,
            11,
            new CreateProcessContextData { FlagsParamIndex = 6, ProcessInformationParamIndex = 10 });

        createProcessTracer.OnFunctionEntered += createProcessTracer_OnFunctionEntered;
        createProcessTracer.OnFunctionExited += createProcessTracer_OnFunctionExited;
        createProcessAsUserTracer.OnFunctionEntered += createProcessTracer_OnFunctionEntered;
        createProcessAsUserTracer.OnFunctionExited += createProcessTracer_OnFunctionExited;

        createProcessTracer.Enable();
        createProcessAsUserTracer.Enable();
      }
    }

    void createProcessTracer_OnFunctionEntered(DkmStackWalkFrame frame, uint[] parameters, object context, out bool suppressExitBreakpoint) {
      CreateProcessContextData cpContext = (CreateProcessContextData)context;
      ProcessCreationFlags flags = (ProcessCreationFlags)parameters[cpContext.FlagsParamIndex];
      suppressExitBreakpoint = !flags.HasFlag(ProcessCreationFlags.CREATE_SUSPENDED);
    }

    void createProcessTracer_OnFunctionExited(DkmStackWalkFrame frame, uint[] parameters, object context) {
      CreateProcessContextData cpContext = (CreateProcessContextData)context;
      HandleCreateProcessExit(frame, parameters[cpContext.ProcessInformationParamIndex]);
    }

    private void HandleCreateProcessExit(DkmStackWalkFrame frame, uint processInformationAddr) {
      // Check the return address first, it should be in EAX.
      uint eax = frame.VscxGetRegisterValue32(CpuRegister.Eax);
      if (eax == 0)
        return;

      DkmProcess process = frame.Process;
      // The process was successfully created.  Extract the PID from the PROCESS_INFORMATION
      // output param.
      int size = Marshal.SizeOf(typeof(PROCESS_INFORMATION));
      byte[] buffer = new byte[size];
      process.ReadMemory(processInformationAddr, DkmReadMemoryFlags.None, buffer);
      PROCESS_INFORMATION info = MarshalUtility.ByteArrayToStructure<PROCESS_INFORMATION>(buffer);
      DkmCustomMessage attachRequest = DkmCustomMessage.Create(
          process.Connection,
          process,
          Guids.Source.VsPackageMessage,
          (int)Messages.Component.MessageCode.AttachToChild,
          new Messages.Component.AttachToChild { ParentId = process.LivePart.Id, ChildId = info.dwProcessId },
          null);
      attachRequest.SendToVsService(PackageServices.DkmComponentEventHandler, false);
    }
  }
}
