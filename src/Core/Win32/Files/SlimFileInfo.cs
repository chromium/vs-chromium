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
    private WIN32_FILE_ATTRIBUTE_DATA _data;
    private int _win32Error = -1;

    public SlimFileInfo(FullPath path) {
      _path = path;
    }

    public bool Exists {
      get {
        EnsureFileAttribues(false);
        return _win32Error == 0;
      }
    }

    public long Length {
      get {
        EnsureFileAttribues(true);
        return HighLowToLong(_data.fileSizeHigh, _data.fileSizeLow);
      }
    }

    public DateTime LastWriteTimeUtc {
      get {
        EnsureFileAttribues(true);
        return DateTime.FromFileTimeUtc(HighLowToLong(_data.ftLastWriteTimeHigh, _data.ftLastWriteTimeLow));
      }
    }

    public FullPath FullPath { get { return _path; } }

    private long HighLowToLong(int high, int low) {
      return HighLowToLong((uint)high, (uint)low);
    }

    private long HighLowToLong(uint high, uint low) {
      return low + ((long)high << 32);
    }

    private void EnsureFileAttribues(bool throwOnError) {
      if (_win32Error == -1)
        Refresh();

      if (_win32Error != 0 && throwOnError)
        throw new Win32Exception(_win32Error);
    }

    private void Refresh() {
      if (!NativeMethods.GetFileAttributesEx(_path.Value, 0, ref _data))
        _win32Error = Marshal.GetLastWin32Error();
      _win32Error = 0;
    }
  }
}
