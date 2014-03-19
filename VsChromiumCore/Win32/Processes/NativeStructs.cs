// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Processes {
  // In general, for all structures below which contains a pointer (represented here by IntPtr),
  // the pointers refer to memory in the address space of the process from which the original
  // structure was read.  While this seems obvious, it means we cannot provide an elegant
  // interface to the various fields in the structure due to the de-reference requiring a
  // handle to the target process.  Instead, that functionality needs to be provided at a
  // higher level.
  //
  // Additionally, since we usually explicitly define the fields that we're interested in along
  // with their respective offsets, we frequently specify the exact size of the native structure.

  // Win32 RTL_USER_PROCESS_PARAMETERS structure.
  [StructLayout(LayoutKind.Explicit, Size = 72)]
  public struct RtlUserProcessParameters {
    [FieldOffset(56)]
    private UnicodeString imagePathName;

    [FieldOffset(64)]
    private UnicodeString commandLine;

    public UnicodeString ImagePathName { get { return imagePathName; } }
    public UnicodeString CommandLine { get { return commandLine; } }
  };

  // Win32 PEB structure.  Represents the process environment block of a process.
  [StructLayout(LayoutKind.Explicit, Size = 472)]
  public struct Peb {
    [FieldOffset(2), MarshalAs(UnmanagedType.U1)]
    private bool isBeingDebugged;

    [FieldOffset(12)]
    private IntPtr ldr;

    [FieldOffset(16)]
    private IntPtr processParameters;

    [FieldOffset(468)]
    private uint sessionId;

    public bool IsBeingDebugged { get { return isBeingDebugged; } }
    public IntPtr Ldr { get { return ldr; } }
    public IntPtr ProcessParameters { get { return processParameters; } }
    public uint SessionId { get { return sessionId; } }
  };

  // Win32 PROCESS_BASIC_INFORMATION.  Contains a pointer to the PEB, and various other
  // information about a process.
  [StructLayout(LayoutKind.Explicit, Size = 24)]
  public struct ProcessBasicInformation {
    [FieldOffset(4)]
    private IntPtr pebBaseAddress;

    [FieldOffset(16)]
    private UIntPtr uniqueProcessId;

    [FieldOffset(20)]
    private IntPtr parentProcessId;

    public IntPtr PebBaseAddress { get { return pebBaseAddress; } }
    public UIntPtr UniqueProcessId { get { return uniqueProcessId; } }
    public IntPtr ParentProcessId { get { return parentProcessId; } }
  }
}
