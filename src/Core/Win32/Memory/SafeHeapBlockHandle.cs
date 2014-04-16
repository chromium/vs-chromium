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
    private readonly IntPtr _processHeapPtr;

    /// <summary>
    /// Note: We only support process heap for now, as using any other heap
    /// would require us to keep a SafeHandle to the heap, and this creates
    /// problems during finalization during .NET CLR shutdown -- referencing and
    /// using a managed object during finalization leads to undeterministic
    /// behavior.
    /// </summary>
    public SafeHeapBlockHandle(SafeProcessHeapHandle heap, IntPtr handle, long byteLength)
      : base(true) {
        _processHeapPtr = heap.DangerousGetHandle();
      _byteLength = byteLength;
      SetHandle(handle);
    }

    public long ByteLength { get { return _byteLength; } }

    public IntPtr Pointer { get { return DangerousGetHandle(); } }

    protected override bool ReleaseHandle() {
      HeapAllocStatic.OnFree(ByteLength);
      return NativeMethods.HeapFree(_processHeapPtr, HeapFlags.Default, handle);
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
