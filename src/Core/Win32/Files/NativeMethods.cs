// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Core.Win32.Files {
  static class NativeMethods {
    /// <summary>
    /// From https://docs.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilea:
    ///
    /// FILE_FLAG_BACKUP_SEMANTICS:
    ///   You must set this flag to obtain a handle to a directory. A directory handle can be
    ///   passed to some functions instead of a file handle. 
    /// </summary>
    public const FileAttributes FILE_FLAG_BACKUP_SEMANTICS = (FileAttributes)0x02000000;

    public enum FINDEX_INFO_LEVELS {
      FindExInfoStandard = 0,
      FindExInfoBasic = 1
    }

    public enum FINDEX_SEARCH_OPS {
      FindExSearchNameMatch = 0,
      FindExSearchLimitToDirectories = 1,
      FindExSearchLimitToDevices = 2
    }

    [Flags]
    public enum FINDEX_ADDITIONAL_FLAGS {
      FindFirstExCaseSensitive = 1,
      FindFirstExLargeFetch = 2,
    }

    public static bool NT_SUCCESS(NTSTATUS ntStatus) {
      return ntStatus >= 0;
    }

    /// <summary>
    /// http://msdn.microsoft.com/en-us/library/windows/desktop/ms681382(v=vs.85).aspx
    /// </summary>
    public enum Win32Errors {
      ERROR_SUCCESS = 0,
      ERROR_INVALID_FUNCTION = 1,
      ERROR_FILE_NOT_FOUND = 2,
      ERROR_PATH_NOT_FOUND = 3,
      ERROR_ACCESS_DENIED = 5,
      ERROR_INVALID_DRIVE = 15,
      ERROR_NO_MORE_FILES = 18,
    }

    public enum NTSTATUS : int {
      STATUS_NO_MORE_FILES = unchecked((int)0x80000006),
      STATUS_INVALID_PARAMETER = unchecked((int)0xC000000D),
      STATUS_NOT_A_DIRECTORY = unchecked((int)0xC0000103),
    }

    [SuppressUnmanagedCodeSecurity]
    [DllImport(@"kernel32", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool ReadFile(
      SafeFileHandle hFile,
      IntPtr pBuffer,
      int numberOfBytesToRead,
      int[] pNumberOfBytesRead,
      NativeOverlapped[] lpOverlapped // should be fixed, if not null
      );

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern SafeFileHandle CreateFile(
      [MarshalAs(UnmanagedType.LPTStr)] string filename,
      // Strangely, System.IO.FileAccess doesn't map directly to the values expected by
      // CreateFile, so we must use our own enum.
      [MarshalAs(UnmanagedType.U4)] NativeAccessFlags access,
      [MarshalAs(UnmanagedType.U4)] FileShare share,
      IntPtr securityAttributes,
      // optional SECURITY_ATTRIBUTES struct or IntPtr.Zero
      [MarshalAs(UnmanagedType.U4)] FileMode creationDisposition,
      [MarshalAs(UnmanagedType.U4)] FileAttributes flagsAndAttributes,
      IntPtr templateFile);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern SafeFindHandle FindFirstFile(string fileName, out WIN32_FIND_DATA data);

    [DllImport("kernel32.dll", BestFitMapping = false, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static unsafe extern SafeFindHandle FindFirstFileEx(
        char* pszPattern,
        FINDEX_INFO_LEVELS fInfoLevelId,
        out WIN32_FIND_DATA lpFindFileData,
        FINDEX_SEARCH_OPS fSearchOp,
        IntPtr lpSearchFilter,
        FINDEX_ADDITIONAL_FLAGS dwAdditionalFlags);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool FindNextFile(SafeFindHandle hndFindFile, out WIN32_FIND_DATA lpFindFileData);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll")]
    internal static extern bool FindClose(IntPtr handle);

    /// <summary>
    /// __kernel_entry NTSYSCALLAPI NTSTATUS NtQueryDirectoryFile(
    ///             HANDLE FileHandle,
    ///             HANDLE Event,
    ///             PIO_APC_ROUTINE ApcRoutine,
    ///             PVOID ApcContext,
    ///             PIO_STATUS_BLOCK IoStatusBlock,
    ///             PVOID FileInformation,
    ///             ULONG Length,
    ///             FILE_INFORMATION_CLASS FileInformationClass,
    ///             BOOLEAN ReturnSingleEntry,
    ///             PUNICODE_STRING FileName,
    ///             BOOLEAN RestartScan
    ///         );
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    [DllImport("ntdll.dll", CharSet = CharSet.Auto, SetLastError = false)]
    internal static extern NTSTATUS NtQueryDirectoryFile(
      SafeFileHandle fileHandle,
      IntPtr eventHandle,
      IntPtr apcRoutime,
      IntPtr appContext,
      ref IO_STATUS_BLOCK ioStatusBlock,
      IntPtr FileInformation,
      UInt32 length,
      FILE_INFORMATION_CLASS fileInformationClass,
      [MarshalAs(UnmanagedType.Bool)]bool returnSingleEntry,
      IntPtr fileName,
      [MarshalAs(UnmanagedType.Bool)]bool restartScan);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("ntdll.dll", CharSet = CharSet.Auto, SetLastError = false)]
    internal static extern uint RtlNtStatusToDosError(NTSTATUS ntStatus);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern bool GetFileAttributesEx(
      string name,
      int fileInfoLevel,
      ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

    [SuppressUnmanagedCodeSecurity]
    [DllImport("kernel32.dll", BestFitMapping = false, CharSet = CharSet.Auto, SetLastError = true)]
    internal static extern FileAttributes GetFileAttributes(string name);

    [StructLayout(LayoutKind.Sequential)]
    internal struct IO_STATUS_BLOCK {
      internal UInt32 status;
      internal IntPtr information;
    }

    internal enum FILE_INFORMATION_CLASS {
      FileDirectoryInformation = 1,
      FileFullDirectoryInformation = 2,
      FileBothDirectoryInformation = 3,
      FileBasicInformation = 4,
      FileStandardInformation = 5,
      FileInternalInformation = 6,
      FileEaInformation = 7,
      FileAccessInformation = 8,
      FileNameInformation = 9,
      FileRenameInformation = 10,
      FileLinkInformation = 11,
      FileNamesInformation = 12,
      FileDispositionInformation = 13,
      FilePositionInformation = 14,
      FileFullEaInformation = 15,
      FileModeInformation = 16,
      FileAlignmentInformation = 17,
      FileAllInformation = 18,
      FileAllocationInformation = 19,
      FileEndOfFileInformation = 20,
      FileAlternateNameInformation = 21,
      FileStreamInformation = 22,
      FilePipeInformation = 23,
      FilePipeLocalInformation = 24,
      FilePipeRemoteInformation = 25,
      FileMailslotQueryInformation = 26,
      FileMailslotSetInformation = 27,
      FileCompressionInformation = 28,
      FileObjectIdInformation = 29,
      FileCompletionInformation = 30,
      FileMoveClusterInformation = 31,
      FileQuotaInformation = 32,
      FileReparsePointInformation = 33,
      FileNetworkOpenInformation = 34,
      FileAttributeTagInformation = 35,
      FileTrackingInformation = 36,
      FileIdBothDirectoryInformation = 37,
      FileIdFullDirectoryInformation = 38,
      FileValidDataLengthInformation = 39,
      FileShortNameInformation = 40,
      FileIoCompletionNotificationInformation = 41,
      FileIoStatusBlockRangeInformation = 42,
      FileIoPriorityHintInformation = 43,
      FileSfioReserveInformation = 44,
      FileSfioVolumeInformation = 45,
      FileHardLinkInformation = 46,
      FileProcessIdsUsingFileInformation = 47,
      FileNormalizedNameInformation = 48,
      FileNetworkPhysicalNameInformation = 49,
      FileMaximumInformation = 50
    }

    /// <summary>
    /// This class defines constants for various byte offsets of the native
    /// FILE_ID_FULL_DIR_INFORMATION data structure.
    /// </summary>
    public static class FILE_ID_FULL_DIR_INFORMATION {
      /**
       * typedef struct _FILE_ID_FULL_DIR_INFORMATION {
       *  ULONG         NextEntryOffset;  // offset = 0
       *  ULONG         FileIndex;        // offset = 4
       *  LARGE_INTEGER CreationTime;     // offset = 8
       *  LARGE_INTEGER LastAccessTime;   // offset = 16
       *  LARGE_INTEGER LastWriteTime;    // offset = 24
       *  LARGE_INTEGER ChangeTime;       // offset = 32
       *  LARGE_INTEGER EndOfFile;        // offset = 40
       *  LARGE_INTEGER AllocationSize;   // offset = 48
       *  ULONG         FileAttributes;   // offset = 56
       *  ULONG         FileNameLength;   // offset = 60
       *  ULONG         EaSize;           // offset = 64
       *  LARGE_INTEGER FileId;           // offset = 72
       *  WCHAR         FileName[1];      // offset = 80
       * } FILE_ID_FULL_DIR_INFORMATION, *PFILE_ID_FULL_DIR_INFORMATION;
       */
      public const int OFFSETOF_NEXT_ENTRY_OFFSET = 0;
      public const int OFFSETOF_CREATION_TIME = 8;
      public const int OFFSETOF_LAST_ACCESS_TIME = 16;
      public const int OFFSETOF_LAST_WRITE_TIME = 24;
      public const int OFFSETOF_END_OF_FILE = 40;
      public const int OFFSETOF_FILE_ATTRIBUTES = 56;
      public const int OFFSETOF_FILENAME_LENGTH = 60;
      public const int OFFSETOF_EA_SIZE = 64;
      public const int OFFSETOF_FILE_ID = 72;
      public const int OFFSETOF_FILENAME = 80;
    }
  }
}
