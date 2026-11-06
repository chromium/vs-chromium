// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;

namespace VsChromium.Bench.Platform;

/// <summary>Win32 functions used by the inventory, the raw file system scenario and the cache flush.</summary>
[SupportedOSPlatform("windows")]
internal static unsafe partial class WindowsNative {
  public const uint FILE_READ_ATTRIBUTES = 0x0080;
  public const uint FILE_SHARE_ALL = 0x7; // Read, write, delete.
  public const uint OPEN_EXISTING = 3;
  public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;

  public const uint FSCTL_QUERY_USN_JOURNAL = 0x000900F4;
  public const uint FSCTL_QUERY_PERSISTENT_VOLUME_STATE = 0x0009023C;
  public const uint IOCTL_STORAGE_QUERY_PROPERTY = 0x002D1400;

  public const int ERROR_INVALID_FUNCTION = 1;
  public const int ERROR_JOURNAL_DELETE_IN_PROGRESS = 1178;
  public const int ERROR_JOURNAL_NOT_ACTIVE = 1179;

  public const uint PERSISTENT_VOLUME_STATE_DEV_VOLUME = 0x00002000;
  public const uint PERSISTENT_VOLUME_STATE_TRUSTED_VOLUME = 0x00004000;

  [StructLayout(LayoutKind.Sequential)]
  public struct USN_JOURNAL_DATA_V0 {
    public ulong UsnJournalID;
    public long FirstUsn;
    public long NextUsn;
    public long LowestValidUsn;
    public long MaxUsn;
    public ulong MaximumSize;
    public ulong AllocationDelta;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct FILE_FS_PERSISTENT_VOLUME_INFORMATION {
    public uint VolumeFlags;
    public uint FlagMask;
    public uint Version;
    public uint Reserved;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_PROPERTY_QUERY {
    public int PropertyId;
    public int QueryType;
    public byte AdditionalParameters;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct DEVICE_SEEK_PENALTY_DESCRIPTOR {
    public uint Version;
    public uint Size;
    public byte IncursSeekPenalty;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct STORAGE_DEVICE_DESCRIPTOR {
    public uint Version;
    public uint Size;
    public byte DeviceType;
    public byte DeviceTypeModifier;
    public byte RemovableMedia;
    public byte CommandQueueing;
    public uint VendorIdOffset;
    public uint ProductIdOffset;
    public uint ProductRevisionOffset;
    public uint SerialNumberOffset;
    public int BusType;
    public uint RawPropertiesLength;
  }

  [LibraryImport("kernel32.dll", EntryPoint = "CreateFileW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
  public static partial SafeFileHandle CreateFile(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
                                                  uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

  [LibraryImport("kernel32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool DeviceIoControl(SafeFileHandle device, uint ioControlCode, void* inBuffer, uint inBufferSize,
                                             void* outBuffer, uint outBufferSize, out uint bytesReturned, IntPtr overlapped);

  [LibraryImport("shell32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool IsUserAnAdmin();

  // Standby list purge (cold file cache, elevated only).
  public const int SystemMemoryListInformation = 80;
  public const int MemoryFlushModifiedList = 3;
  public const int MemoryPurgeStandbyList = 4;
  public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
  public const uint TOKEN_QUERY = 0x0008;
  public const uint SE_PRIVILEGE_ENABLED = 0x00000002;

  [StructLayout(LayoutKind.Sequential)]
  public struct LUID {
    public uint LowPart;
    public int HighPart;
  }

  [StructLayout(LayoutKind.Sequential)]
  public struct TOKEN_PRIVILEGES {
    public uint PrivilegeCount;
    public LUID Luid;
    public uint Attributes;
  }

  [LibraryImport("advapi32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

  [LibraryImport("advapi32.dll", EntryPoint = "LookupPrivilegeValueW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool LookupPrivilegeValue(string? systemName, string name, out LUID luid);

  [LibraryImport("advapi32.dll", SetLastError = true)]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool AdjustTokenPrivileges(IntPtr tokenHandle, [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
                                                   ref TOKEN_PRIVILEGES newState, uint bufferLength, IntPtr previousState, IntPtr returnLength);

  [LibraryImport("kernel32.dll")]
  public static partial IntPtr GetCurrentProcess();

  [LibraryImport("kernel32.dll")]
  [return: MarshalAs(UnmanagedType.Bool)]
  public static partial bool CloseHandle(IntPtr handle);

  [LibraryImport("ntdll.dll")]
  public static partial int NtSetSystemInformation(int systemInformationClass, void* systemInformation, int systemInformationLength);

  /// <summary>Opens a file or a directory for its attributes only (no data access).</summary>
  public static SafeFileHandle OpenForAttributes(string path) =>
    CreateFile(path, FILE_READ_ATTRIBUTES, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING, FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
}
