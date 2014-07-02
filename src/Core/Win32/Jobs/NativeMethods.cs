// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Win32.Jobs {
  static class NativeMethods {
    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateJobObject(IntPtr lpJobAttributes, string lpName);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll")]
    public static extern bool SetInformationJobObject(
      SafeFileHandle hJob,
      JobObjectInfoType infoType,
      IntPtr lpJobObjectInfo,
      uint cbJobObjectInfoLength);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool IsProcessInJob(IntPtr processHandle, IntPtr jobHandle, out bool result);
  }
}
