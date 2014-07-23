// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Breakpoints;
using Microsoft.VisualStudio.Debugger.Native;

namespace VsChromium.DkmIntegration.ServerComponent {
  static class DkmProcessExtensions {

    public static void VscxOnModuleInstanceLoad(this DkmProcess process, DkmModuleInstance module, DkmWorkList workList) {
      AutoAttachToChildHandler handler = process.GetDataItem<AutoAttachToChildHandler>();
      if (handler == null || module.TagValue != DkmModuleInstance.Tag.NativeModuleInstance)
        return;

      handler.OnModuleInstanceLoad((DkmNativeModuleInstance)module, workList);
    }

    public static void VscxOnRuntimeBreakpointReceived(
        this DkmProcess process,
        DkmRuntimeBreakpoint bp,
        DkmThread thread,
        bool hasException,
        DkmEventDescriptorS eventDescriptor) {
      RuntimeBreakpointHandler handler = process.GetDataItem<RuntimeBreakpointHandler>();
      if (handler == null)
        return;

      handler.OnRuntimeBreakpointReceived(bp, thread, hasException, eventDescriptor);
    }
  }
}
