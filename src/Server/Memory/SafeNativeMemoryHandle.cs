// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Memory {
  public sealed class SafeNativeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid {
    private readonly int _byteLength;

    public SafeNativeMemoryHandle(IntPtr handle, int byteLength)
      : base(true) {
      _byteLength = byteLength;
      SetHandle(handle);
    }

    public int ByteLength { get { return _byteLength; } }

    public IntPtr Pointer { get { return DangerousGetHandle(); } }

    protected override bool ReleaseHandle() {
      NativeHeap.OnFree(ByteLength);
      unsafe {
        NativeMemory.Free(handle.ToPointer());
      }
      return true;
    }
  }
}
