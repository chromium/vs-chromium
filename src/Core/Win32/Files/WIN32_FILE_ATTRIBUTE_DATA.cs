// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;

namespace VsChromium.Core.Win32.Files {
  [Flags]
  public enum FILE_ATTRIBUTE : uint {
    FILE_ATTRIBUTE_READONLY = 0x00000001,
    FILE_ATTRIBUTE_HIDDEN = 0x00000002,
    FILE_ATTRIBUTE_SYSTEM = 0x00000004,
    FILE_ATTRIBUTE_DIRECTORY = 0x00000010,
    FILE_ATTRIBUTE_ARCHIVE = 0x00000020,
    FILE_ATTRIBUTE_DEVICE = 0x00000040,
    FILE_ATTRIBUTE_NORMAL = 0x00000080,
    FILE_ATTRIBUTE_TEMPORARY = 0x00000100,
    FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200,
    FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400,
    FILE_ATTRIBUTE_COMPRESSED = 0x00000800,
    FILE_ATTRIBUTE_OFFLINE = 0x00001000,
    FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000,
    FILE_ATTRIBUTE_ENCRYPTED = 0x00004000,
  }

  [Serializable]
  [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
  public struct WIN32_FILE_ATTRIBUTE_DATA {
    public int fileAttributes;
    public uint ftCreationTimeLow;
    public uint ftCreationTimeHigh;
    public uint ftLastAccessTimeLow;
    public uint ftLastAccessTimeHigh;
    public uint ftLastWriteTimeLow;
    public uint ftLastWriteTimeHigh;
    public int fileSizeHigh;
    public int fileSizeLow;
  }
}
