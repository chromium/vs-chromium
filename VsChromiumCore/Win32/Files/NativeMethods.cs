// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace VsChromiumCore.Win32.Files {
  static class NativeMethods {
    [SuppressUnmanagedCodeSecurity]
    [DllImport(@"kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool ReadFile(
      SafeFileHandle hFile,
      IntPtr pBuffer,
      int numberOfBytesToRead,
      int[] pNumberOfBytesRead,
      NativeOverlapped[] lpOverlapped // should be fixed, if not null
      );

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
      [MarshalAs(UnmanagedType.LPTStr)] string filename,
      // Strangely, System.IO.FileAccess doesn't map directly to the values expected by
      // CreateFile, so we must use our own enum.
      [MarshalAs(UnmanagedType.U4)] NativeAccessFlags access,
      [MarshalAs(UnmanagedType.U4)] FileShare share,
      IntPtr securityAttributes,
      // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
      [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
      [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
      IntPtr templateFile);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern SafeFindHandle FindFirstFile(string fileName, out WIN32_FIND_DATA data);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool FindNextFile(SafeFindHandle hndFindFile, out WIN32_FIND_DATA lpFindFileData);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll")]
    internal static extern bool FindClose(IntPtr handle);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool GetFileAttributesEx(
      string name,
      int fileInfoLevel,
      ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);
  }
}
