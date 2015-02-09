// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using VsChromium.Core.DkmShared;
using VsChromium.Core.Logging;

namespace VsChromium.DkmIntegration.ServerComponent {
  // Visual Studio's debugger component model is a layered one, similar to a driver filter chain,
  // whereby components are notified either from lowest to highest or highest to lowest depending
  // on the operation.  Components are further sub-categorized according to their level into
  // "server components" and "ide components".  This is done to support remote debugging, although
  // the same separation exists even for local debugging.  Certain debug engine operations 
  // (typically opeartions involving low level access to the debug engine) can only be performed in
  // the context of a server component (ComponentLevel < 100000), while other operations can only
  // be performed from the context of an IDE component.
  //
  // This class implements the server component.
  class ServerComponent
      : IDkmRuntimeInstanceLoadNotification
      , IDkmModuleInstanceLoadNotification
      , IDkmRuntimeBreakpointReceived
      , IDkmCustomMessageForwardReceiver {
    public ServerComponent() {
    }

    public void OnRuntimeInstanceLoad(DkmRuntimeInstance runtimeInstance, DkmEventDescriptor eventDescriptor) {
      runtimeInstance.VscxOnRuntimeInstanceLoad();
    }

    public void OnModuleInstanceLoad(DkmModuleInstance moduleInstance, DkmWorkList workList, DkmEventDescriptorS eventDescriptor) {
      moduleInstance.Process.VscxOnModuleInstanceLoad(moduleInstance, workList);
    }

    void IDkmRuntimeBreakpointReceived.OnRuntimeBreakpointReceived(DkmRuntimeBreakpoint bp, DkmThread thread, bool hasException, DkmEventDescriptorS eventDescriptor) {
      DkmProcess process = bp.Process;
      process.VscxOnRuntimeBreakpointReceived(bp, thread, hasException, eventDescriptor);
    }

    public DkmCustomMessage SendLower(DkmCustomMessage customMessage) {
      try {
        Debug.Assert(customMessage.SourceId == PackageServices.VsDebuggerMessageGuid);
        VsDebuggerMessage code = (VsDebuggerMessage)customMessage.MessageCode;
        switch (code) {
          case VsDebuggerMessage.EnableChildProcessDebugging:
            Guid processGuid = (Guid)customMessage.Parameter1;
            DkmProcess process = DkmProcess.FindProcess(processGuid);
            if (process != null) {
              process.SetDataItem(
                  DkmDataCreationDisposition.CreateNew, 
                  new RuntimeBreakpointHandler());
              process.SetDataItem(
                  DkmDataCreationDisposition.CreateNew, 
                  new AutoAttachToChildHandler());
              Logger.LogInfo(
                "Successfully delay-enabled child debugging for process {0}.", 
                processGuid);
            } else {
              Logger.LogError(
                  "Unable to find process {0} while trying to enable child process debugging.", 
                  processGuid);
            }
            break;
          default:
            Logger.LogError("Debug component received unknown message code {0}.", code);
            break;
        }
      } catch (DkmException exception) {
        Logger.LogError(
            exception, 
            "An error occurred while handling a debugger message.  HR = 0x{0:X}",
            exception.HResult);
      }
      return null;
    }
  }
}
