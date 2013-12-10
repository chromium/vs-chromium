// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Security;

namespace VsChromiumCore.Win32.Memory {
  static class NativeMethods {
    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32", SetLastError = true)]
    public static extern SafeProcessHeapHandle GetProcessHeap();

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32", SetLastError = true)]
    public static extern SafeHeapHandle HeapCreate(HeapFlags flOptions, IntPtr dwInitialSize, IntPtr dwMaximumSize);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32")]
    public static extern IntPtr HeapAlloc(SafeHeapHandle hHeap, HeapFlags flags, IntPtr size);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32")]
    public static extern bool HeapFree(SafeHeapHandle hHeap, HeapFlags flags, IntPtr block);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32", SetLastError = true)]
    public static extern bool HeapDestroy(SafeHeapHandle hHeap);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll")]
    public static extern IntPtr LocalFree(IntPtr hMem);
  }
}
