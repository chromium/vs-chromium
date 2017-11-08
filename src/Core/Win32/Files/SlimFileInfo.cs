// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using VsChromium.Core.Files;

namespace VsChromium.Core.Win32.Files {
  /// <summary>
  /// SlimFileInfo is, as the name suggests, a slimmer version of System.IO.FileInfo. The intent
  /// is to be more efficient because of fewer checks performed.
  /// </summary>
  public struct SlimFileInfo {
    private readonly FullPath _path;
    private readonly WIN32_FILE_ATTRIBUTE_DATA _data;
    private readonly int _win32Error;

    public SlimFileInfo(FullPath path, WIN32_FILE_ATTRIBUTE_DATA data, int win32Error) {
      _path = path;
      _data = data;
      _win32Error = win32Error;
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

    public bool IsSymLink {
      get {
        return _win32Error == 0 &&
          IsSymLinkImpl((FILE_ATTRIBUTE)_data.fileAttributes);
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

    private void ThrowOnError() {
      if (_win32Error != (int) NativeFile.Win32Errors.ERROR_SUCCESS) {
        try {
          throw new Win32Exception(_win32Error);
        }
        catch (Exception e) {
          throw new Exception(string.Format("Error reading attributes of file \"{0}\"", _path), e);
        }
      }
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

    private static bool IsSymLinkImpl(FILE_ATTRIBUTE data) {
      return (data & FILE_ATTRIBUTE.FILE_ATTRIBUTE_REPARSE_POINT) != 0;
    }
  }
}
