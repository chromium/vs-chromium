// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers {
  // Analyzes basic stdcall stack frames, where all parameters are passed on the stack.  This is
  // the most common type for all Windows APIs.
  public class StdcallFrameAnalyzer : StackFrameAnalyzer {

    public StdcallFrameAnalyzer(IEnumerable<FunctionParameter> parameters) 
        : base(parameters) {
    }

    public override object GetArgumentValue(DkmStackWalkFrame frame, int index) {
      ulong esp = (uint)frame.Registers.GetStackPointer();
      uint esp2 = frame.VscxGetRegisterValue32(CpuRegister.Esp);
      uint ebp = frame.VscxGetRegisterValue32(CpuRegister.Ebp);
      ulong frameBase = (uint)frame.FrameBase;

      int stackOffset = 0;
      for (int i = 0; i < index; ++i)
        stackOffset += _parameters[i].GetPaddedSize(WordSize);

      // The return address (4 bytes) is at the top of the stack, so offset by 4 to skip the return
      // address.
      ulong stackAddress = esp + 4 + (ulong)stackOffset;
      int paramSize = _parameters[index].GetSize(WordSize);
      byte[] parameter = new byte[paramSize];

      frame.Process.ReadMemory(stackAddress, DkmReadMemoryFlags.None, parameter);
      switch (paramSize) {
        case 4:
          return BitConverter.ToUInt32(parameter, 0);
        case 8:
          return BitConverter.ToUInt64(parameter, 0);
        default:
          return parameter;
      }
    }

    public override int WordSize {
      get { return 4; }
    }
  }
}
