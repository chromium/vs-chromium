// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Files {
  [BestFitMapping(false)]
  [Serializable]
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
  internal struct WIN32_FIND_DATA {
    internal int dwFileAttributes;
    internal int ftCreationTime_dwLowDateTime;
    internal int ftCreationTime_dwHighDateTime;
    internal int ftLastAccessTime_dwLowDateTime;
    internal int ftLastAccessTime_dwHighDateTime;
    internal int ftLastWriteTime_dwLowDateTime;
    internal int ftLastWriteTime_dwHighDateTime;
    internal int nFileSizeHigh;
    internal int nFileSizeLow;
    internal int dwReserved0;
    internal int dwReserved1;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
    internal string cFileName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
    internal string cAlternateFileName;
  }
}
