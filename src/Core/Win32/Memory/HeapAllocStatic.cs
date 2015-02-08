// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;

namespace VsChromium.Core.Win32.Memory {
  public static class HeapAllocStatic {
    public static readonly IntPtr ProcessHeapPtr = NativeMethods.GetProcessHeap().DangerousGetHandle();
    private static long _totalMemory;

    public static long TotalMemory { get { return _totalMemory; } }

    public static SafeHeapBlockHandle Alloc(int size) {
      IntPtr block = NativeMethods.HeapAlloc(ProcessHeapPtr, HeapFlags.Default, new IntPtr(size));
      if (block == IntPtr.Zero)
        throw new OutOfMemoryException();

      OnAlloc(size);
      return new SafeHeapBlockHandle(block, size);
    }

    public static void OnAlloc(int size) {
      Interlocked.Add(ref _totalMemory, size);
    }

    public static void OnFree(int size) {
      Interlocked.Add(ref _totalMemory, -size);
    }
  }
}
