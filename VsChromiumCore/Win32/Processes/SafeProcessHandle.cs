// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.Win32.SafeHandles;

namespace VsChromiumCore.Win32.Processes {
  sealed class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid {
    internal SafeProcessHandle()
      : base(true) {
    }

    internal SafeProcessHandle(IntPtr handle)
      : base(true) {
      base.SetHandle(handle);
    }

    internal void InitialSetHandle(IntPtr handlePtr) {
      handle = handlePtr;
    }

    protected override bool ReleaseHandle() {
      return Handles.NativeMethods.CloseHandle(handle);
    }
  }
}
