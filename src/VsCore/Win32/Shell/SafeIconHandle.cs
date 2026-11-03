// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Win32.Shell {
  public sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid {
    public SafeIconHandle(IntPtr hIcon)
      : base(true) {
      SetHandle(hIcon);
    }

    protected override bool ReleaseHandle() {
      return DestroyIcon(handle);
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool DestroyIcon(IntPtr hIcon);
  }
}