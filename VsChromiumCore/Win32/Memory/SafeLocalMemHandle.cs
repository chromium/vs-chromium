// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.Win32.SafeHandles;

namespace VsChromiumCore.Win32.Memory {
  internal sealed class SafeLocalMemHandle : SafeHandleZeroOrMinusOneIsInvalid {
    public SafeLocalMemHandle()
        : base(true) {
    }

    public SafeLocalMemHandle(IntPtr existingHandle, bool ownsHandle)
        : base(ownsHandle) {
      base.SetHandle(existingHandle);
    }

    protected override bool ReleaseHandle() {
      return NativeMethods.LocalFree(handle) == IntPtr.Zero;
    }
  }
}
