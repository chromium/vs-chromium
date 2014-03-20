// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Shell {
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
  public struct SHFileInfo {
    // C# doesn't support overriding the default constructor of value types, so we need to use
    // a dummy constructor.
    public SHFileInfo(bool dummy) {
      hIcon = IntPtr.Zero;
      iIcon = 0;
      dwAttributes = 0;
      szDisplayName = "";
      szTypeName = "";
    }

    public IntPtr hIcon;
    public int iIcon;
    public uint dwAttributes;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    public string szDisplayName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
    public string szTypeName;
  };
}
