// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Utility;

namespace VsChromium.DkmIntegration.ServerComponent {
  static class DkmRuntimeInstanceExtensions {
    public static void VscxOnRuntimeInstanceLoad(this DkmRuntimeInstance runtime) {
      if (runtime.TagValue != DkmRuntimeInstance.Tag.NativeRuntimeInstance)
        return;

      DkmNativeRuntimeInstance nativeRuntime = (DkmNativeRuntimeInstance)runtime;
      bool shouldHandle = false;
      bool forceAutoAttach = false;

      // Check if the process is a chrome executable, and if so, attach a CallstackFilter to the DkmProcess.
      if (ChromeUtility.IsChromeProcess(nativeRuntime.Process.Path))
        shouldHandle = true;
#if DEBUG
      else {
        string fileName = Path.GetFileName(nativeRuntime.Process.Path);
        if (fileName.Equals("vistest.exe", StringComparison.CurrentCultureIgnoreCase)) {
          forceAutoAttach = true;
          shouldHandle = true;
        }
      }
#endif

      if (shouldHandle) {
        DkmProcess process = nativeRuntime.Process;
        DebugProcessOptions options = DebugProcessOptions.Create(process.DebugLaunchSettings.OptionsString);
        ProcessDebugOptionsDataItem optionsDataItem = new ProcessDebugOptionsDataItem(options);
        process.SetDataItem(DkmDataCreationDisposition.CreateAlways, optionsDataItem);
        process.SetDataItem(DkmDataCreationDisposition.CreateAlways, new RuntimeBreakpointHandler());
        if (forceAutoAttach || (options.AutoAttachToChildren &&
            !process.SystemInformation.Flags.HasFlag(DkmSystemInformationFlags.Is64Bit))) {
          process.SetDataItem(DkmDataCreationDisposition.CreateAlways, new AutoAttachToChildHandler());
        }
      }
    }
  }
}
