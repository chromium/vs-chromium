// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.Native;
using VsChromium.Core.DkmShared;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.DkmIntegration.ServerComponent {
  static class DkmRuntimeInstanceExtensions {
    public static void VscxOnRuntimeInstanceLoad(this DkmRuntimeInstance runtime) {
      if (runtime.TagValue != DkmRuntimeInstance.Tag.NativeRuntimeInstance)
        return;

      DkmNativeRuntimeInstance nativeRuntime = (DkmNativeRuntimeInstance)runtime;
      bool isChrome = false;
      bool isTestProcess = false;

      // Check if the process is a chrome executable, and if so, attach a CallstackFilter to the DkmProcess.
      if (ChromeUtility.IsChromeProcess(nativeRuntime.Process.Path))
        isChrome = true;
#if DEBUG
      string fileName = Path.GetFileName(nativeRuntime.Process.Path);
      if (fileName.Equals("vistest.exe", StringComparison.CurrentCultureIgnoreCase))
        isTestProcess = true;
#endif

      if (isTestProcess || isChrome) {
        DkmProcess process = nativeRuntime.Process;

        DebugProcessOptions options = DebugProcessOptions.Create(process.DebugLaunchSettings.OptionsString);
        ProcessDebugOptionsDataItem optionsDataItem = new ProcessDebugOptionsDataItem(options);
        process.SetDataItem(DkmDataCreationDisposition.CreateAlways, optionsDataItem);

        if (isTestProcess || ShouldEnableChildDebugging(nativeRuntime.Process, options)) {
          process.SetDataItem(DkmDataCreationDisposition.CreateAlways, new RuntimeBreakpointHandler());
          process.SetDataItem(DkmDataCreationDisposition.CreateAlways, new AutoAttachToChildHandler());
        }
      }
    }

    private static bool ShouldEnableChildDebugging(DkmProcess process, DebugProcessOptions options) {
      if (options.ChildDebuggingMode == ChildDebuggingMode.AlwaysAttach)
        return true;

      if (options.ChildDebuggingMode == ChildDebuggingMode.UseDefault) {
        Logger.LogInfo(
            "Requesting default child process debugging mode for process {0}.", 
            process.UniqueId);
        DkmCustomMessage attachRequest = DkmCustomMessage.Create(
            process.Connection,
            process,
            PackageServices.VsPackageMessageGuid,
            (int)VsPackageMessage.IsChildDebuggingEnabled,
            process.UniqueId,
            process.Connection.UniqueId);
        // This needs to happen synchronously in order to guarantee that child debugging is enabled
        // before the process finishes initializing.
        attachRequest.SendToVsService(PackageServices.DkmComponentEventHandler, true);
      }
      return false;
    }
  }
}
