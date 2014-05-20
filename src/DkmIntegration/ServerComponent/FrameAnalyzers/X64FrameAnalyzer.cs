// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Debugger;
using Microsoft.VisualStudio.Debugger.CallStack;
using Microsoft.VisualStudio.Debugger.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VsChromium.DkmIntegration.ServerComponent.FrameAnalyzers {
  // Analyzes stack frames of x64 calling conventions.  This class handles only the case of x64
  // frames that use the default convention for MS Windows API exported functions.  Additionally,
  // it assumes that the function being analyzed accepts only integral arguments, and as such
  // does not make use of additional floating point registers.
  public class X64FrameAnalyzer : StackFrameAnalyzer {
    public X64FrameAnalyzer(IEnumerable<FunctionParameter> parameters) 
        : base(parameters) {
    }

    public override object GetArgumentValue(DkmStackWalkFrame frame, int index) {
      ulong rsp = frame.Registers.GetStackPointer();
      ulong rsp2 = frame.VscxGetRegisterValue64(CpuRegister.Rsp);
      ulong rbp = frame.VscxGetRegisterValue64(CpuRegister.Rbp);
      ulong frameBase = frame.FrameBase;

      byte[] argumentBuffer = ReadArgumentBytes(frame);

      int wordZeroIndex = 0;
      for (int i=0; i < index; ++i)
        wordZeroIndex += _parameters[i].GetPaddedSize(WordSize);
      FunctionParameter requestedParameter = _parameters[index];
      int requestedParamSize = requestedParameter.GetSize(WordSize);
      byte[] result = new byte[requestedParamSize];

      Array.ConstrainedCopy(argumentBuffer, wordZeroIndex, result, 0, requestedParamSize);

      switch (requestedParamSize) {
        case 4:
          return BitConverter.ToUInt32(result, 0);
        case 8:
          return BitConverter.ToUInt64(result, 0);
        default:
          return result;
      }
    }

    public override int WordSize {
      get { return 8; }
    }

    private byte[] ReadArgumentBytes(DkmStackWalkFrame frame) {
      // In the x64 calling convention, the first 4 arguments are passed in registers, and the 
      // remaining arguments are passed on the stack.  However, space for all arguments is
      // allocated on the stack, even if the argument is passed one of the first 4 which is passed 
      // by register.  Furthermore, a minimum of 4*WordSize bytes is allocated on the stack for
      // function parameters, even if the function accepts less than 4 arguments.
      int bytesRequired = 0;
      foreach (FunctionParameter param in _parameters)
        bytesRequired += param.GetPaddedSize(WordSize);
      byte[] stack = new byte[bytesRequired];

      ulong rsp = frame.Registers.GetStackPointer();
      // The first word on the stack is the return address, similar to in x86 calling conventions.
      frame.Process.ReadMemory(rsp + 8, DkmReadMemoryFlags.None, stack);

      // Since everything is padded, we should have a multiple-of-8 bytes.  The first four
      // arguments may or may not actually be on the stack since it's register spill-over space,
      // but they are guaranteed to be in RCX, RDX, R8, and R9 respectively.  So get their values 
      // from the registers and then write them into our copy of the stack.
      Debug.Assert(bytesRequired % 8 == 0);
      if (bytesRequired > 0) {
        ulong rcx = frame.VscxGetRegisterValue64(CpuRegister.Rcx);
        BitConverter.GetBytes(rcx).CopyTo(stack, 0);
      }
      if (bytesRequired > 8) {
        ulong rdx = frame.VscxGetRegisterValue64(CpuRegister.Rdx);
        BitConverter.GetBytes(rdx).CopyTo(stack, 8);
      }
      if (bytesRequired > 16) {
        ulong r8 = frame.VscxGetRegisterValue64(CpuRegister.R8);
        BitConverter.GetBytes(r8).CopyTo(stack, 16);
      }
      if (bytesRequired > 24) {
        ulong r9 = frame.VscxGetRegisterValue64(CpuRegister.R9);
        BitConverter.GetBytes(r9).CopyTo(stack, 24);
      }

      return stack;
    }
  }
}
