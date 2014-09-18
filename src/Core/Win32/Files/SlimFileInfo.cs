// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using VsChromium.Core.Files;

namespace VsChromium.Core.Win32.Files {
  /// <summary>
  /// SlimFileInfo is, as the name suggests, a slimmer version of System.IO.FileInfo. The intent
  /// is to be more efficient because of fewer checks performed.
  /// </summary>
  public class SlimFileInfo {
    private readonly FullPath _path;
    private readonly WIN32_FILE_ATTRIBUTE_DATA _data;
    private readonly int _win32Error;

    public SlimFileInfo(FullPath path) {
      _path = path;
      _win32Error = EnsureFileAttribues(out _data);
    }

    public FullPath FullPath { get { return _path; } }

    public bool IsFile {
      get {
        return _win32Error == 0 &&
          IsFileImpl((FILE_ATTRIBUTE)_data.fileAttributes);
      }
    }

    public bool IsDirectory {
      get {
        return _win32Error == 0 &&
          IsDirectoryImpl((FILE_ATTRIBUTE)_data.fileAttributes);
      }
    }

    public bool Exists {
      get {
        return _win32Error == 0;
      }
    }

    public long Length {
      get {
        ThrowOnError();
        return HighLowToLong(_data.fileSizeHigh, _data.fileSizeLow);
      }
    }

    public DateTime LastWriteTimeUtc {
      get {
        ThrowOnError();
        return DateTime.FromFileTimeUtc(HighLowToLong(_data.ftLastWriteTimeHigh, _data.ftLastWriteTimeLow));
      }
    }

    private int EnsureFileAttribues(out WIN32_FILE_ATTRIBUTE_DATA data) {
      var win32Error = 0;
      data = default(WIN32_FILE_ATTRIBUTE_DATA);
      if (!NativeMethods.GetFileAttributesEx(_path.Value, 0, ref data))
        win32Error = Marshal.GetLastWin32Error();
      return win32Error;
    }

    private void ThrowOnError() {
      if (_win32Error != (int)NativeFile.Win32Errors.ERROR_SUCCESS)
        throw new Win32Exception(_win32Error);
    }

    private static long HighLowToLong(int high, int low) {
      return HighLowToLong((uint)high, (uint)low);
    }

    private static long HighLowToLong(uint high, uint low) {
      return low + ((long)high << 32);
    }

    private static bool IsFileImpl(FILE_ATTRIBUTE data) {
      return (data & FILE_ATTRIBUTE.FILE_ATTRIBUTE_DIRECTORY) == 0;
    }

    private static bool IsDirectoryImpl(FILE_ATTRIBUTE data) {
      return (data & FILE_ATTRIBUTE.FILE_ATTRIBUTE_DIRECTORY) != 0;
    }
  }
}
