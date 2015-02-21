// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Win32.Files {
  public sealed class SafeFindHandle : SafeHandleZeroOrMinusOneIsInvalid {
    internal SafeFindHandle()
      : base(true) {
    }

    protected override bool ReleaseHandle() {
      return NativeMethods.FindClose(handle);
    }
  }
}
