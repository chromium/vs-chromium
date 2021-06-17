﻿// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.DefaultPort;
using Microsoft.VisualStudio.Debugger.Interop;
using VsChromium.Core.DkmShared;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.ToolsOptions;

namespace VsChromium.Features.AttachToChrome {
  [Guid(PackageServices.DkmComponentEventHandlerId)]
  class DkmComponentEventHandler : IVsCustomDebuggerEventHandler110 {
    private readonly IVisualStudioPackage _package;

    public DkmComponentEventHandler(IVisualStudioPackage package) {
      _package = package;
    }

    private bool IsChildDebuggingEnabledByDefault {
      get {
        return _package.GetToolsOptionsPage<DebuggingOptions>().EnableChildDebugging;
      }
    }
    public int OnCustomDebugEvent(ref Guid ProcessId, VsComponentMessage message) {
      try {
        VsPackageMessage code = (VsPackageMessage)message.MessageCode;
        switch (code) {
          case VsPackageMessage.AttachToChild:
            int parentId = (int)message.Parameter1;
            int childId = (int)message.Parameter2;
            Process proc = Process.GetProcessById(childId);
            if (proc != null)
              DebugAttach.AttachToProcess(new Process[] { proc }, ChildDebuggingMode.AlwaysAttach);
            break;
          case VsPackageMessage.IsChildDebuggingEnabled:
            Guid processGuid = (Guid)message.Parameter1;
            Guid connectionGuid = (Guid)message.Parameter2;
            DkmTransportConnection connection = DkmTransportConnection.FindConnection(connectionGuid);
            if (connection != null) {
              if (IsChildDebuggingEnabledByDefault) {
                DkmCustomMessage response = DkmCustomMessage.Create(
                    connection,
                    null,
                    PackageServices.VsDebuggerMessageGuid,
                    (int)VsDebuggerMessage.EnableChildProcessDebugging,
                    processGuid,
                    null
                );
                response.SendLower();
              } else {
                Logger.LogInfo(
                    "Not enabling child process debugging for process {0}.  " +
                    "Child debugging is disabled by default.",
                    processGuid);
              }
            }
            break;
        }
      } catch (Exception exception) {
        Logger.LogError(
            exception,
            "An error occured while handling a VsPackage message.  HR=0x{0:X}", 
            exception.HResult);
      }
      return 0;
    }
  }
}
