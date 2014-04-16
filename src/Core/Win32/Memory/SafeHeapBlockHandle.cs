// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Win32.Memory {
  public class SafeHeapBlockHandle : SafeHandleZeroOrMinusOneIsInvalid {
    private readonly long _byteLength;
    /// <summary>
    /// Keep a reference to the heap to prevent it from being destroyed during
    /// normal GC operations.
    /// </summary>
    private readonly SafeHeapHandle _heap;

    public SafeHeapBlockHandle(SafeHeapHandle heap, IntPtr handle, long byteLength)
      : base(true) {
      _heap = heap;
      _byteLength = byteLength;
      SetHandle(handle);
    }

    public long ByteLength { get { return _byteLength; } }

    public IntPtr Pointer { get { return DangerousGetHandle(); } }

    protected override bool ReleaseHandle() {
      HeapAllocStatic.OnFree(ByteLength);
      // During finalization, the heap may be destroyed before us. This
      // implictly releases all memory associcated to the heap, so it is ok to
      // not call HeapFree in that case.
      // TODO(rpaquay): Using methods on other managed objects (_heap) is not
      // valid during finalization, so this code is in theory incorrect. Is it
      // in practice?
      if (!_heap.IsClosed) {
        return NativeMethods.HeapFree(_heap.DangerousGetHandle(), HeapFlags.Default, handle);
      }
      return true;
    }

    /// <summary>
    /// Note: This is merely for debugging purposes.
    /// </summary>
    public byte[] ToArray() {
      var buf = new byte[ByteLength];
      Marshal.Copy(Pointer, buf, 0, buf.Length);
      return buf;
    }
  }
}
