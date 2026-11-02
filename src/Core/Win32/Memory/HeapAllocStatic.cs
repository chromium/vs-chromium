// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;

namespace VsChromium.Core.Win32.Memory {
  /// <summary>
  /// Allocation of unmanaged memory blocks (file contents), with accounting of
  /// the total allocated size. On the server (modern .NET), blocks are
  /// allocated with the portable <c>NativeMemory</c>
  /// API; on the Visual Studio side (.NET Framework), from the Win32 process heap.
  /// </summary>
  public static class HeapAllocStatic {
#if !NETCOREAPP
    public static readonly IntPtr ProcessHeapPtr = NativeMethods.GetProcessHeap().DangerousGetHandle();
#endif
    private static long _totalMemory;

    public static long TotalMemory { get { return _totalMemory; } }

    public static SafeHeapBlockHandle Alloc(int size) {
#if NETCOREAPP
      // Note: NativeMemory.Alloc throws OutOfMemoryException on failure.
      IntPtr block;
      unsafe {
        block = new IntPtr(System.Runtime.InteropServices.NativeMemory.Alloc((nuint)Math.Max(size, 1)));
      }
#else
      IntPtr block = NativeMethods.HeapAlloc(ProcessHeapPtr, HeapFlags.Default, new IntPtr(size));
      if (block == IntPtr.Zero)
        throw new OutOfMemoryException();
#endif

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
