// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.ComponentInterfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Win32.Processes;

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
      , IDkmRuntimeBreakpointReceived {
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
  }
}
