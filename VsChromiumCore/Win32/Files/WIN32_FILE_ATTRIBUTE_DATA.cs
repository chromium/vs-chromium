// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Files {
  [Serializable]
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
  struct WIN32_FILE_ATTRIBUTE_DATA {
    internal int fileAttributes;
    internal uint ftCreationTimeLow;
    internal uint ftCreationTimeHigh;
    internal uint ftLastAccessTimeLow;
    internal uint ftLastAccessTimeHigh;
    internal uint ftLastWriteTimeLow;
    internal uint ftLastWriteTimeHigh;
    internal int fileSizeHigh;
    internal int fileSizeLow;
  }
}
