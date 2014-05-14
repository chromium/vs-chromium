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
      int stackOffset = 0;
      for (int i = 0; i < index; ++i)
        stackOffset += _parameters[i].PaddedSize;

      uint ebp = frame.VscxGetRegisterValue32(CpuRegister.Ebp);

      // stdcall stores two pointers at 4 bytes each at the top of the stack, so adding 8 targets
      // the first function parameter.
      ulong stackAddress = ebp + 8 + (ulong)stackOffset;
      int paramSize = _parameters[index].Size;
      byte[] parameter = new byte[paramSize];

      frame.Process.ReadMemory(stackAddress, DkmReadMemoryFlags.None, parameter);
      if (paramSize == 4)
        return BitConverter.ToUInt32(parameter, 0);
      else
        return parameter;
    }

    public override ulong PrologueLength {
      get { return 5; }
    }
  }
}
