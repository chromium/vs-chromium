// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace VsChromium.Core.Memory {
  /// <summary>
  /// Allocation of unmanaged memory blocks (file contents) via the portable
  /// <see cref="NativeMemory"/> API, with accounting of the total allocated size.
  /// </summary>
  public static class NativeHeap {
    private static long _totalMemory;

    public static long TotalMemory { get { return Volatile.Read(ref _totalMemory); } }

    public static SafeNativeMemoryHandle Alloc(int size) {
      // Note: NativeMemory.Alloc throws OutOfMemoryException on failure.
      IntPtr block;
      unsafe {
        block = new IntPtr(NativeMemory.Alloc((nuint)Math.Max(size, 1)));
      }

      OnAlloc(size);
      return new SafeNativeMemoryHandle(block, size);
    }

    internal static void OnAlloc(int size) {
      Interlocked.Add(ref _totalMemory, size);
    }

    internal static void OnFree(int size) {
      Interlocked.Add(ref _totalMemory, -size);
    }
  }
}
