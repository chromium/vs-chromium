// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Memory;
using static VsChromium.Core.Win32.Files.NativeMethods;

namespace VsChromium.Core.Win32.Files {
  public static class NativeFile {
    /// <summary>
    /// Note: For testability, this function should be called through <see cref="IFileSystem"/>.
    /// </summary>
    public static SafeHeapBlockHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) {
      var result = ReadFileWorker(path, fileSize, trailingByteCount);

      var trailingPtr = result.Pointer.ToInt64() + result.ByteLength - trailingByteCount;
      for (var i = 0; i < trailingByteCount; i++) {
        Marshal.WriteByte(new IntPtr(trailingPtr + i), 0);
      }
      return result;
    }

    private static SafeHeapBlockHandle ReadFileWorker(FullPath path, long fileSize, int trailingByteCount) {
      using (
        var fileHandle = NativeMethods.CreateFile(path.Value,
          NativeAccessFlags.GenericRead,
          FileShare.ReadWrite | FileShare.Delete,
          IntPtr.Zero,
          FileMode.Open,
          0,
          IntPtr.Zero)) {
        if (fileHandle.IsInvalid) {
          throw new Win32Exception();
        }

        // Note: We are limited to 2GB files by design.
        var maxLen = int.MaxValue - trailingByteCount;
        if (fileSize >= maxLen) {
          Logger.LogWarn("File too big, truncated to {0} bytes", maxLen);
        }
        var len = (int)Math.Min(maxLen, fileSize);
        var heap = HeapAllocStatic.Alloc(len + trailingByteCount);
        try {
          var bytesRead = new int[1];

          if (!NativeMethods.ReadFile(fileHandle, heap.Pointer, len, bytesRead, null))
            throw new Win32Exception();

          if (bytesRead[0] != len)
            throw new Exception("File read operation didn't read the whole file");
        } catch (Exception) {
          heap.Dispose();
          throw;
        }

        return heap;
      }
    }

    /// <summary>
    /// Note: For testability, this function should be called through <see cref="IFileSystem"/>.
    /// </summary>
    public static unsafe List<DirectoryEntry> GetDirectoryEntries(string path) {
      var directoryEntries = new List<DirectoryEntry>();

      // Open the file with the special FILE_LIST_DIRECTORY access to enable reading
      // the contents of the directory file (i.e. the list of directory entries).
      // Note that the FILE_FLAG_BACKUP_SEMANTICS is also important to ensure this
      // call succeeds.
      var fileHandle = NativeMethods.CreateFile(path,
        NativeAccessFlags.FILE_LIST_DIRECTORY,
        FileShare.Read | FileShare.Write | FileShare.Delete,
        IntPtr.Zero,
        FileMode.Open,
        NativeMethods.FILE_FLAG_BACKUP_SEMANTICS,
        IntPtr.Zero);
      if (fileHandle.IsInvalid) {
        var lastWin32Error = Marshal.GetLastWin32Error();
        throw new LastWin32ErrorException(lastWin32Error,
          string.Format("Error enumerating files at \"{0}\".", path));
      }

      using (fileHandle) {
        // 8KB is large enough to hold about 80 entries of average size (the size depends on the
        // length of the filename), which is a reasonable compromise in terms of stack usages
        // vs # of calls to the API.
        const int bufferSize = 8192;
        byte* bufferAddress = stackalloc byte[bufferSize];

        // Invoke NtQueryDirectoryFile to fill the initial buffer
        NTSTATUS status = InvokeNtQueryDirectoryFile(fileHandle, bufferAddress, bufferSize);
        if (!NativeMethods.NT_SUCCESS(status)) {
          // On the first invokcation, NtQueryDirectoryFile returns STATUS_INVALID_PARAMETER when
          // asked to enumerate an invalid directory (ie it is a file
          // instead of a directory).  Verify that is the actual cause
          // of the error.
          if (status == NTSTATUS.STATUS_INVALID_PARAMETER) {
            FileAttributes attributes = NativeMethods.GetFileAttributes(path);
            if ((attributes & FileAttributes.Directory) == 0) {
              status = NTSTATUS.STATUS_NOT_A_DIRECTORY;
            }
          }

          throw ThrowInvokeNtQueryDirectoryFileError(path, status);
        }

        // Process entries from the buffer, and invoke NtQueryDirectoryFile as long as there are
        // more entries to enumerate.
        while (true) {
          ProcessFileInformationBuffer(directoryEntries, bufferAddress, bufferSize);

          status = InvokeNtQueryDirectoryFile(fileHandle, bufferAddress, bufferSize);
          if (!NativeMethods.NT_SUCCESS(status)) {
            if (status == NTSTATUS.STATUS_NO_MORE_FILES) {
              // Success, enumeration finished
              break;
            } else {
              throw ThrowInvokeNtQueryDirectoryFileError(path, status);
            }
          }
        }
      } // using fileHandle

      return directoryEntries;
    }

    /// <summary>
    /// Invoke the <see cref="NativeMethods.NtQueryDirectoryFile"/> function with parameters
    /// adequate for retrieving the next set of <see cref="FILE_ID_FULL_DIR_INFORMATION"/>
    /// entries
    /// </summary>
    private static unsafe NTSTATUS InvokeNtQueryDirectoryFile(SafeFileHandle fileHandle, byte* bufferAddress, int bufferSize) {
      IO_STATUS_BLOCK statusBlock = new IO_STATUS_BLOCK();

      NTSTATUS status = NativeMethods.NtQueryDirectoryFile(
        fileHandle, // FileHandle
        IntPtr.Zero, // Event
        IntPtr.Zero, // ApcRoutine
        IntPtr.Zero, // ApcContext
        ref statusBlock, // IoStatusBlock
        new IntPtr(bufferAddress), // FileInformation
        (uint)bufferSize, // Length
        FILE_INFORMATION_CLASS.FileIdFullDirectoryInformation, // FileInformationClass
        false, // ReturnSingleEntry
        IntPtr.Zero, // FileName
        false); // RestartScan

      return status;
    }

    private static unsafe Exception ThrowInvokeNtQueryDirectoryFileError(string path, NTSTATUS status) {
      uint win32ErrorCode = NativeMethods.RtlNtStatusToDosError(status);
      throw new LastWin32ErrorException((int)win32ErrorCode,
        string.Format("Error during enumeration of files at \"{0}\".", path));
    }

    /// <summary>
    /// Process all instances of <see cref="FILE_ID_FULL_DIR_INFORMATION"/> stored in memory
    /// <paramref name="currentBuffer"/>.
    /// </summary>
    private static unsafe void ProcessFileInformationBuffer(List<DirectoryEntry> directoryEntries, byte* buffer, int bufferSize) {
      var currentBuffer = buffer;
      var endBuffer = buffer + bufferSize;
      while (true) {
        // Check buffer overrun
        if (currentBuffer + FILE_ID_FULL_DIR_INFORMATION.OFFSETOF_FILENAME_LENGTH > endBuffer) {
          throw new InvalidDataException("The buffer from NtQueryDirectoryFile is too small or contains invalid data");
        }

        // Add entry from current offset
        string fileName = getFileNameFromFileIdFullDirInformation(currentBuffer);
        var fileAttrs = (FILE_ATTRIBUTE)GetInt(currentBuffer + FILE_ID_FULL_DIR_INFORMATION.OFFSETOF_FILE_ATTRIBUTES);
        AddDirectoryEntry(directoryEntries, fileName, fileAttrs);

        // Go to next entry (if there is one)
        int nextOffset = GetInt(currentBuffer + FILE_ID_FULL_DIR_INFORMATION.OFFSETOF_NEXT_ENTRY_OFFSET);
        if (nextOffset == 0) {
          break;
        }
        currentBuffer += nextOffset;
      }
    }

    private static unsafe String getFileNameFromFileIdFullDirInformation(byte* buffer) {
      // Read the character count
      int nameLengthInBytes = GetInt(buffer + FILE_ID_FULL_DIR_INFORMATION.OFFSETOF_FILENAME_LENGTH);
      if ((nameLengthInBytes % 2) != 0) {
        throw new InvalidDataException("FileNameLength is not a multiple of 2");
      }

      // Create the string
      return GetString(buffer + FILE_ID_FULL_DIR_INFORMATION.OFFSETOF_FILENAME, nameLengthInBytes / 2);
    }

    private static unsafe int GetInt(byte* buffer) {
      return *(int*)buffer;
    }

    private static unsafe string GetString(byte* buffer, int charCount) {
      return new String((char*)buffer, 0, charCount);
    }

    private static void AddDirectoryEntry(List<DirectoryEntry> entries, string fileName, FILE_ATTRIBUTE attrs) {
      var entry = new DirectoryEntry(fileName, attrs);
      if (SkipSpecialEntry(entry))
        return;

      entries.Add(entry);
    }

    private static bool SkipSpecialEntry(DirectoryEntry entry) {
      return (entry.IsDirectory) && (entry.Name.Equals(".") || entry.Name.Equals(".."));
    }
  }
}
