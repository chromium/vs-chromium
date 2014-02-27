// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Text;
using VsChromiumCore.Win32.Interop;

namespace VsChromiumCore.Win32.Processes {
  static class NativeMethods {
    [DllImport("kernel32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool CreateProcess(
      [MarshalAs(UnmanagedType.LPTStr)] string lpApplicationName,
      StringBuilder lpCommandLine,
      SecurityAttributes lpProcessAttributes,
      SecurityAttributes lpThreadAttributes,
      bool bInheritHandles,
      ProcessCreationFlags dwCreationFlags,
      IntPtr lpEnvironment,
      [MarshalAs(UnmanagedType.LPTStr)] string lpCurrentDirectory,
      STARTUPINFO lpStartupInfo,
      [In, Out]
      PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool ReadProcessMemory(SafeProcessHandle hProcess, IntPtr lpBaseAddress, [Out] byte[] buffer, UInt32 size, out UInt32 lpNumberOfBytesRead);
  }
}
