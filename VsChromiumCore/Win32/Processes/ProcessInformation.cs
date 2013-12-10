// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Processes {
  [StructLayout(LayoutKind.Sequential)]
  class ProcessInformation {
    public IntPtr hProcess = IntPtr.Zero;
    public IntPtr hThread = IntPtr.Zero;
    public int dwProcessId;
    public int dwThreadId;
  }
}
