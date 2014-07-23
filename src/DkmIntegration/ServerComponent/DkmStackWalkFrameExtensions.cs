// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Debugger.CallStack;

namespace VsChromium.DkmIntegration.ServerComponent {
  static class DkmStackWalkFrameExtensions {
    public static ulong VscxGetReturnAddress(this DkmStackWalkFrame frame) {
      ulong ret;
      ulong frameBase;
      ulong vframe;
      frame.Thread.GetCurrentFrameInfo(out ret, out frameBase, out vframe);
      return ret;
    }

    public static uint VscxGetRegisterValue32(this DkmStackWalkFrame frame, CpuRegister reg) {
      byte[] buffer = new byte[4];
      frame.Registers.GetRegisterValue((uint)reg, buffer);
      return BitConverter.ToUInt32(buffer, 0);
    }

    public static ulong VscxGetRegisterValue64(this DkmStackWalkFrame frame, CpuRegister reg) {
      byte[] buffer = new byte[8];
      frame.Registers.GetRegisterValue((uint)reg, buffer);
      return BitConverter.ToUInt64(buffer, 0);
    }
  }
}
