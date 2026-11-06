// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Files.Windows {
  /// <summary>
  /// The Win32 and NT APIs used by the Windows specific file system code (directory enumeration with
  /// file ids, NTFS/ReFS change journal). Source generated marshalling only (<see
  /// cref="LibraryImportAttribute"/>): compatible with trimming and NativeAOT.
  /// </summary>
  [SupportedOSPlatform("windows")]
  internal static unsafe partial class WindowsNativeMethods {
    public const uint GENERIC_READ = 0x80000000;
    public const uint FILE_LIST_DIRECTORY = 0x0001;
    public const uint FILE_READ_ATTRIBUTES = 0x0080;
    public const uint FILE_SHARE_ALL = 0x00000007; // Read, write, delete
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    public const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    public const int ERROR_FILE_NOT_FOUND = 2;
    public const int ERROR_PATH_NOT_FOUND = 3;
    public const int ERROR_ACCESS_DENIED = 5;
    public const int ERROR_INVALID_FUNCTION = 1;
    public const int ERROR_NOT_SUPPORTED = 50;
    public const int ERROR_INVALID_PARAMETER = 87;
    public const int ERROR_JOURNAL_DELETE_IN_PROGRESS = 1178;
    public const int ERROR_JOURNAL_NOT_ACTIVE = 1179;
    public const int ERROR_JOURNAL_ENTRY_DELETED = 1181;

    public const int STATUS_SUCCESS = 0;
    public const int STATUS_NO_MORE_FILES = unchecked((int)0x80000006);
    public const int STATUS_NO_SUCH_FILE = unchecked((int)0xC000000F);
    public const int STATUS_INVALID_INFO_CLASS = unchecked((int)0xC0000003);
    public const int STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D);
    public const int STATUS_NOT_SUPPORTED = unchecked((int)0xC00000BB);

    /// <summary>FILE_INFORMATION_CLASS: FILE_ID_EXTD_DIR_INFORMATION (NTFS, ReFS).</summary>
    public const int FileIdExtdDirectoryInformation = 60;

    /// <summary>FILE_INFO_BY_HANDLE_CLASS: FILE_ID_INFO.</summary>
    public const int FileIdInfo = 18;

    public const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
    public const uint FSCTL_READ_UNPRIVILEGED_USN_JOURNAL = 0x000903ab;

    [StructLayout(LayoutKind.Sequential)]
    public struct IO_STATUS_BLOCK {
      public IntPtr Status;
      public UIntPtr Information;
    }

    /// <summary>FILE_ID_INFO.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FILE_ID_INFO {
      public ulong VolumeSerialNumber;
      public ulong FileIdLow;
      public ulong FileIdHigh;
    }

    /// <summary>USN_JOURNAL_DATA_V2 (the first fields are the ones of V0 and V1).</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct USN_JOURNAL_DATA {
      public ulong UsnJournalID;
      public long FirstUsn;
      public long NextUsn;
      public long LowestValidUsn;
      public long MaxUsn;
      public ulong MaximumSize;
      public ulong AllocationDelta;
      public ushort MinSupportedMajorVersion;
      public ushort MaxSupportedMajorVersion;
      public uint Flags;
      public ulong RangeTrackChunkSize;
      public long RangeTrackFileSizeThreshold;
    }

    /// <summary>READ_USN_JOURNAL_DATA_V1.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct READ_USN_JOURNAL_DATA {
      public long StartUsn;
      public uint ReasonMask;
      public uint ReturnOnlyOnClose;
      public ulong Timeout;
      public ulong BytesToWaitFor;
      public ulong UsnJournalID;
      public ushort MinMajorVersion;
      public ushort MaxMajorVersion;
    }

    [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    public static partial SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
                                                    uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [LibraryImport("ntdll.dll")]
    public static partial int NtQueryDirectoryFile(SafeFileHandle fileHandle, IntPtr eventHandle, IntPtr apcRoutine, IntPtr apcContext,
                                                   out IO_STATUS_BLOCK ioStatusBlock, void* fileInformation, uint length,
                                                   int fileInformationClass, byte returnSingleEntry, IntPtr fileName,
                                                   byte restartScan);

    [LibraryImport("ntdll.dll")]
    public static partial uint RtlNtStatusToDosError(int status);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetFileInformationByHandleEx(SafeFileHandle file, int fileInformationClass, void* fileInformation,
                                                            uint bufferSize);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, void* inBuffer, uint inBufferSize,
                                               void* outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);

    [LibraryImport("kernel32.dll", EntryPoint = "GetVolumeInformationByHandleW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetVolumeInformationByHandle(SafeFileHandle file, char* volumeName, uint volumeNameSize,
                                                            out uint volumeSerialNumber, out uint maximumComponentLength,
                                                            out uint fileSystemFlags, char* fileSystemName, uint fileSystemNameSize);

    /// <summary>
    /// The path to pass to the Win32 APIs: long paths (more than MAX_PATH characters) need the
    /// <c>\\?\</c> prefix (the .NET APIs add it themselves).
    /// </summary>
    public static string ToNativePath(string path) {
      if (path.Length < 248 || path.StartsWith(@"\\?\", StringComparison.Ordinal))
        return path;
      if (path.StartsWith(@"\\", StringComparison.Ordinal))
        return @"\\?\UNC\" + path.Substring(2);
      return @"\\?\" + path;
    }

    /// <summary>
    /// Opens a file or a directory (without following a final reparse point if <paramref
    /// name="openReparsePoint"/>), throws an exception if it fails.
    /// </summary>
    public static SafeFileHandle OpenHandle(string path, uint access, bool openReparsePoint = false) {
      var handle = CreateFile(ToNativePath(path), access, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | (openReparsePoint ? FILE_FLAG_OPEN_REPARSE_POINT : 0), IntPtr.Zero);
      if (handle.IsInvalid) {
        var error = Marshal.GetLastPInvokeError();
        handle.Dispose();
        throw CreateException(error, path);
      }
      return handle;
    }

    /// <summary>The exception for a Win32 error, of the same type as the .NET file APIs.</summary>
    public static Exception CreateException(int error, string path) {
      var message = string.Format("{0} : '{1}'", new Win32Exception(error).Message, path);
      switch (error) {
        case ERROR_FILE_NOT_FOUND:
          return new FileNotFoundException(message, path);
        case ERROR_PATH_NOT_FOUND:
          return new DirectoryNotFoundException(message);
        case ERROR_ACCESS_DENIED:
          return new UnauthorizedAccessException(message);
        default:
          return new IOException(message, unchecked((int)0x80070000 | error));
      }
    }

    /// <summary>The 128-bit id and the volume serial number of an open file or directory.</summary>
    public static bool TryGetFileId(SafeFileHandle handle, out ulong volumeSerialNumber, out UInt128 fileId) {
      FILE_ID_INFO info;
      if (!GetFileInformationByHandleEx(handle, FileIdInfo, &info, (uint)sizeof(FILE_ID_INFO))) {
        volumeSerialNumber = 0;
        fileId = default;
        return false;
      }
      volumeSerialNumber = info.VolumeSerialNumber;
      fileId = new UInt128(info.FileIdHigh, info.FileIdLow);
      return true;
    }
  }
}
