// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Win32.Files {
  public static class NativeFile {
    // http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382(v=vs.85).aspx
    public enum Win32Errors {
      ERROR_SUCCESS = 0,
      ERROR_INVALID_FUNCTION = 1,
      ERROR_FILE_NOT_FOUND = 2,
      ERROR_PATH_NOT_FOUND = 3,
      ERROR_ACCESS_DENIED = 5,
      ERROR_INVALID_DRIVE = 15,
      ERROR_NO_MORE_FILES = 18,
    }

    /// <summary>
    /// Note: For testability, this function should be called through <see cref="IFileSystem"/>.
    /// </summary>
    public static SafeHeapBlockHandle ReadFileNulTerminated(SlimFileInfo fileInfo, int trailingByteCount) {
      var result = ReadFileWorker(fileInfo, trailingByteCount);

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
        int maxLen = Int32.MaxValue - trailingByteCount;
        if (fileInfo.Length >= maxLen) {
          Logger.Log("File too big, truncated to {0} bytes", maxLen);
        }
        var len = (int)Math.Min(maxLen, fileInfo.Length);
        var heap = HeapAllocStatic.Alloc(len + trailingByteCount);
        var bytesRead = new int[1];

        if (!NativeMethods.ReadFile(fileHandle, heap.Pointer, len, bytesRead, null))
          throw new Win32Exception();

        if (bytesRead[0] != len)
          throw new Exception("File read operation didn't read the whole file");

        return heap;
      }
    }

    /// <summary>
    /// Note: For testability, this function should be called through <see cref="IFileSystem"/>.
    /// </summary>
    public static unsafe List<DirectoryEntry> GetDirectoryEntries(string path) {
      // Buidl search pattern (on the stack) as path + "\\*" + '\0'
      var charCount = path.Length + 2 + 1;
      char* patternBuffer = stackalloc char[charCount];
      char* dest = patternBuffer;
      fixed (char* pathPtr = path) {
        char* src = pathPtr;
        while ((*dest = *src) != 0) {
          dest++;
          src++;
        }
      }
      *dest++ = '\\';
      *dest++ = '*';
      *dest++ = '\0';

      var result = new List<DirectoryEntry>();

      // Start enumerating files
      WIN32_FIND_DATA data;
      var findHandle = NativeMethods.FindFirstFileEx(
        patternBuffer,
        NativeMethods.FINDEX_INFO_LEVELS.FindExInfoBasic,
        out data,
        NativeMethods.FINDEX_SEARCH_OPS.FindExSearchNameMatch,
        IntPtr.Zero,
        NativeMethods.FINDEX_ADDITIONAL_FLAGS.FindFirstExLargeFetch);
        if (findHandle.IsInvalid) {
          var lastWin32Error = Marshal.GetLastWin32Error();
          if (lastWin32Error != (int)Win32Errors.ERROR_FILE_NOT_FOUND &&
              lastWin32Error != (int)Win32Errors.ERROR_PATH_NOT_FOUND &&
              lastWin32Error != (int)Win32Errors.ERROR_ACCESS_DENIED &&
              lastWin32Error != (int)Win32Errors.ERROR_NO_MORE_FILES) {
            throw new LastWin32ErrorException(lastWin32Error, 
              string.Format("Error enumerating files at \"{0}\".", path));
          }
          return result;
        }

      using (findHandle) {
        AddResult(ref data, result);
        while (NativeMethods.FindNextFile(findHandle, out data)) {
          AddResult(ref data, result);
        }
        var lastWin32Error = Marshal.GetLastWin32Error();
        if (lastWin32Error != (int)Win32Errors.ERROR_SUCCESS &&
            lastWin32Error != (int)Win32Errors.ERROR_FILE_NOT_FOUND &&
            lastWin32Error != (int)Win32Errors.ERROR_PATH_NOT_FOUND &&
            lastWin32Error != (int)Win32Errors.ERROR_NO_MORE_FILES) {
          throw new LastWin32ErrorException(lastWin32Error,
            string.Format("Error during enumeration of files at \"{0}\".", path));
        }
      }
      return result;
    }

    private static void AddResult(ref WIN32_FIND_DATA data, List<DirectoryEntry> entries) {
      var entry = new DirectoryEntry(data.cFileName, (FILE_ATTRIBUTE)data.dwFileAttributes);
      if (SkipSpecialEntry(entry))
        return;

      entries.Add(entry);
    }

    private static bool SkipSpecialEntry(DirectoryEntry entry) {
      return (entry.IsDirectory) && (entry.Name.Equals(".") || entry.Name.Equals(".."));
    }
  }
}
