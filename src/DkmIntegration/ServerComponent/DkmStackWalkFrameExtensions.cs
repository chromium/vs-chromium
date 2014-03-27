// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent {
  static class DkmStackWalkFrameExtensions {
    public static uint[] VscxAnalyzeFunctionParams(this DkmStackWalkFrame frame, uint count) {
      uint[] parameters = new uint[count];
      // TODO: Make this work for x64.
      ulong esp = frame.Registers.GetStackPointer();
      ulong arg = esp + 8;
      for (int i = 0; i < count; ++i) {
        byte[] buffer = new byte[4];
        frame.Process.ReadMemory(arg, DkmReadMemoryFlags.None, buffer);
        parameters[i] = BitConverter.ToUInt32(buffer, 0);
        arg += 4;
      }
      return parameters;
    }

    public static ulong VscxGetArgumentStackLocation(this DkmStackWalkFrame frame, uint index) {
      ulong esp = frame.Registers.GetStackPointer();
      ulong arg0 = esp + 8;
      return arg0 + index * 4;
    }

    public static uint VscxGetArgumentValue(this DkmStackWalkFrame frame, uint index) {
      ulong location = VscxGetArgumentStackLocation(frame, index);
      byte[] result = new byte[4];
      frame.Process.ReadMemory(location, DkmReadMemoryFlags.None, result);
      return BitConverter.ToUInt32(result, 0);
    }

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
  }
}
