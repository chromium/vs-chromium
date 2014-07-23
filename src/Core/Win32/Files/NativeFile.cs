﻿// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Win32.Files {
  public static class NativeFile {
    public static SafeHeapBlockHandle ReadFileNulTerminated(SlimFileInfo fileInfo, int trailingByteCount) {
      var result = ReadFileWorker(fileInfo, trailingByteCount);

      // Use of WriteInt16 is tied to "2" bytes.
      var trailingPtr = result.Pointer.ToInt64() + result.ByteLength - trailingByteCount;
      for (var i = 0; i < trailingByteCount; i++) {
        Marshal.WriteByte(new IntPtr(trailingPtr + i), 0);
      }
      return result;
    }

    private static SafeHeapBlockHandle ReadFileWorker(SlimFileInfo fileInfo, int trailingByteCount) {
      using (
        var fileHandle = NativeMethods.CreateFile(fileInfo.FullPath.Value, NativeAccessFlags.GenericRead, FileShare.Read, IntPtr.Zero,
                                                  FileMode.Open, 0, IntPtr.Zero)) {
        if (fileHandle.IsInvalid)
          throw new Win32Exception();

        // Note: We are limited to 2GB files by design.
        var len = (int)fileInfo.Length;
        var heap = HeapAllocStatic.Alloc(len + trailingByteCount);
        var bytesRead = new int[1];

        if (!NativeMethods.ReadFile(fileHandle, heap.Pointer, len, bytesRead, null))
          throw new Win32Exception();

        if (bytesRead[0] != len)
          throw new Exception("File read operation didn't read the whole file");

        return heap;
      }
    }

    public static void GetDirectoryEntries(string path, out IList<string> directories, out IList<string> files) {
      var pattern = path + "\\*";

      files = new List<string>();
      directories = new List<string>();

      WIN32_FIND_DATA data;
      using (var handle = NativeMethods.FindFirstFile(pattern, out data)) {
        if (handle.IsInvalid) {
          var lastWin32Error = Marshal.GetLastWin32Error();
          if (lastWin32Error != 2 && lastWin32Error != 18 && lastWin32Error != 3) {
            throw new LastWin32ErrorException(lastWin32Error, string.Format("Error getting first entry of file entries for path \"{0}\".", path));
          }
          return;
        }

        AddResult(ref data, directories, files);
        while (NativeMethods.FindNextFile(handle, out data)) {
          AddResult(ref data, directories, files);
        }
        var lastWin32Error2 = Marshal.GetLastWin32Error();
        if (lastWin32Error2 != 0 && lastWin32Error2 != 18 && lastWin32Error2 != 2) {
          throw new LastWin32ErrorException(lastWin32Error2, string.Format("Error getting next entry of file entries for path \"{0}\".", path));
        }
      }
    }

    private static void AddResult(ref WIN32_FIND_DATA data, IList<string> directories, IList<string> files) {
      if (IsFile(ref data))
        files.Add(data.cFileName);
      else if (IsDir(ref data))
        directories.Add(data.cFileName);
    }

    internal static bool IsFile(ref WIN32_FIND_DATA data) {
      return 0 == (data.dwFileAttributes & 16);
    }

    internal static bool IsDir(ref WIN32_FIND_DATA data) {
      return (data.dwFileAttributes & 16) != 0 && !data.cFileName.Equals(".") && !data.cFileName.Equals("..");
    }
  }
}
