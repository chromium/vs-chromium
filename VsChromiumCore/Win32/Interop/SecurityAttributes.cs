// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using VsChromiumCore.Win32.Memory;

namespace VsChromiumCore.Win32.Interop {
  [StructLayout(LayoutKind.Sequential)]
  class SecurityAttributes {
    public int nLength = 12;
    public SafeLocalMemHandle lpSecurityDescriptor = new SafeLocalMemHandle(IntPtr.Zero, false);
    [MarshalAs(UnmanagedType.Bool)]
    public bool bInheritHandle;
  }
}
