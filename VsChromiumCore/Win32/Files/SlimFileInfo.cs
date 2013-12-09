// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace VsChromiumCore.Win32.Files {
  /// <summary>
  /// SlimFileInfo is, as the name suggests, a slimmer version of System.IO.FileInfo. The intent
  /// is to be more efficient because of fewer checks performed.
  /// </summary>
  public class SlimFileInfo {
    private readonly string _path;
    private WIN32_FILE_ATTRIBUTE_DATA _data;
    private int _win32Error = -1;

    public SlimFileInfo(string path) {
      this._path = path;
    }

    public bool Exists {
      get {
        EnsureFileAttribues(false);
        return this._win32Error == 0;
      }
    }

    public long Length {
      get {
        EnsureFileAttribues(true);
        return HighLowToLong(this._data.fileSizeHigh, this._data.fileSizeLow);
      }
    }

    public DateTime LastWriteTimeUtc {
      get {
        EnsureFileAttribues(true);
        return DateTime.FromFileTimeUtc(HighLowToLong(this._data.ftLastWriteTimeHigh, this._data.ftLastWriteTimeLow));
      }
    }

    public string FullName {
      get {
        return this._path;
      }
    }

    private long HighLowToLong(int high, int low) {
      return HighLowToLong((uint)high, (uint)low);
    }

    private long HighLowToLong(uint high, uint low) {
      return low + ((long)high << 32);
    }

    private void EnsureFileAttribues(bool throwOnError) {
      if (this._win32Error == -1)
        Refresh();

      if (this._win32Error != 0 && throwOnError)
        throw new Win32Exception(this._win32Error);
    }

    private void Refresh() {
      if (!NativeMethods.GetFileAttributesEx(this._path, 0, ref this._data))
        this._win32Error = Marshal.GetLastWin32Error();
      this._win32Error = 0;
    }
  }
}
