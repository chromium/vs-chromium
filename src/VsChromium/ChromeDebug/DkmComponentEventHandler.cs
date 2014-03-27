// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core;
using VsChromium.DkmIntegration.Messages.Component;

namespace VsChromium.ChromeDebug {
  [Guid(PackageServices.DkmComponentEventHandlerId)]
  class DkmComponentEventHandler : IVsCustomDebuggerEventHandler110 {
    public int OnCustomDebugEvent(ref Guid ProcessId, VsComponentMessage message) {
      MessageCode code = (MessageCode)message.MessageCode;
      switch (code) {
        case MessageCode.AttachToChild:
          AttachToChild attachMessage = (AttachToChild)message.Parameter1;
          Process proc = Process.GetProcessById(attachMessage.ChildId);
          if (proc != null)
            DebugAttach.AttachToProcess(new Process[] { proc }, true);
          return 0;
        default:
          return 0;
      }
    }
  }
}
