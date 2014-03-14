// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Win32.SafeHandles;

namespace VsChromiumServer.NativeInterop {
  public sealed class SafeSearchHandle : SafeHandleZeroOrMinusOneIsInvalid {
    internal SafeSearchHandle()
      : base(true) {
    }

    protected override bool ReleaseHandle() {
      NativeMethods.AsciiSearchAlgorithm_Delete(handle);
      return true;
    }
  }
}
