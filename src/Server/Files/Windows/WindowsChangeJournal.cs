// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using static VsChromium.Core.Files.Windows.WindowsNativeMethods;

namespace VsChromium.Core.Files.Windows {
  /// <summary>
  /// The NTFS/ReFS change journal (USN journal), read without privilege: through a handle to a
  /// directory of the volume (the project root) with <c>FSCTL_QUERY_USN_JOURNAL</c> and
  /// <c>FSCTL_READ_UNPRIVILEGED_USN_JOURNAL</c> (Windows 10 1709+). A handle to the volume needs
  /// administrator rights. The unprivileged records have no file name: only the file id, the parent
  /// directory id, the reasons and the attributes, which is all the catch-up needs (see
  /// <c>ChangeJournalCatchUp</c>). See "Windows specific code" in PORTING_PRINCIPLES.md.
  /// </summary>
  [SupportedOSPlatform("windows")]
  public sealed class WindowsChangeJournal : IChangeJournal {
    private const int ReadBufferSize = 256 * 1024;

    public ChangeJournalQueryResult Query(FullPath directory) {
      if (directory.Value.StartsWith(@"\\", StringComparison.Ordinal))
        return ChangeJournalQueryResult.Failure($"\"{directory}\" is on a network share");
      var root = Path.GetPathRoot(directory.Value);
      if (!string.IsNullOrEmpty(root) && GetDriveType(root) == DRIVE_REMOTE)
        return ChangeJournalQueryResult.Failure($"\"{directory}\" is on a network drive");
      try {
        using (var handle = OpenHandle(directory.Value, FILE_READ_ATTRIBUTES)) {
          var fileSystemName = GetFileSystemName(handle);
          if (fileSystemName != null && fileSystemName != "NTFS" && fileSystemName != "ReFS")
            return ChangeJournalQueryResult.Failure($"\"{root}\" is a {fileSystemName} volume (NTFS or ReFS only)", fileSystemName);
          if (!TryGetFileId(handle, out var volumeSerialNumber, out _))
            return ChangeJournalQueryResult.Failure("the volume serial number of \"" + directory + "\" can't be read: " +
              new Win32Exception(Marshal.GetLastPInvokeError()).Message, fileSystemName);
          if (!TryQuery(handle, out var data, out var error))
            return ChangeJournalQueryResult.Failure(DescribeError(error, root), fileSystemName);
          return ChangeJournalQueryResult.Success(new ChangeJournalCheckpoint(volumeSerialNumber, data.UsnJournalID, data.NextUsn),
            data.FirstUsn, fileSystemName);
        }
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        return ChangeJournalQueryResult.Failure(e.Message);
      }
    }

    public unsafe void ReadRecords(FullPath directory, ChangeJournalCheckpoint start, long endUsn, ChangeJournalRecordHandler handler,
                                   CancellationToken cancellationToken) {
      SafeFileHandle handle;
      try {
        handle = OpenHandle(directory.Value, FILE_READ_ATTRIBUTES);
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        throw new ChangeJournalException(e.Message, e);
      }
      using (handle) {
        if (!TryGetFileId(handle, out var volumeSerialNumber, out _) || volumeSerialNumber != start.VolumeSerialNumber)
          throw new ChangeJournalException("another volume");
        var buffer = GC.AllocateUninitializedArray<byte>(ReadBufferSize, pinned: true);
        var usn = start.Usn;
        fixed (byte* output = buffer) {
          while (usn < endUsn) {
            cancellationToken.ThrowIfCancellationRequested();
            var input = new READ_USN_JOURNAL_DATA {
              StartUsn = usn,
              ReasonMask = 0xFFFFFFFF,
              ReturnOnlyOnClose = 0,
              Timeout = 0,
              BytesToWaitFor = 0,
              UsnJournalID = start.JournalId,
              MinMajorVersion = 2,
              MaxMajorVersion = 4,
            };
            if (!DeviceIoControl(handle, FSCTL_READ_UNPRIVILEGED_USN_JOURNAL, &input, (uint)sizeof(READ_USN_JOURNAL_DATA), output,
                  ReadBufferSize, out var bytesReturned, IntPtr.Zero)) {
              throw new ChangeJournalException(DescribeError(Marshal.GetLastPInvokeError(), Path.GetPathRoot(directory.Value)));
            }
            var nextUsn = UsnRecordParser.Parse(new ReadOnlySpan<byte>(output, (int)bytesReturned), handler);
            // No more records (end of the journal).
            if (bytesReturned <= UsnRecordParser.OutputHeaderSize || nextUsn <= usn)
              break;
            usn = nextUsn;
          }
        }
      }
    }

    private static unsafe bool TryQuery(SafeFileHandle handle, out USN_JOURNAL_DATA data, out int error) {
      USN_JOURNAL_DATA result;
      if (!DeviceIoControl(handle, FSCTL_QUERY_USN_JOURNAL, null, 0, &result, (uint)sizeof(USN_JOURNAL_DATA), out _, IntPtr.Zero)) {
        error = Marshal.GetLastPInvokeError();
        data = default;
        return false;
      }
      error = 0;
      data = result;
      return true;
    }

    private static unsafe string GetFileSystemName(SafeFileHandle handle) {
      const int size = 64;
      var name = stackalloc char[size];
      if (!GetVolumeInformationByHandle(handle, null, 0, out _, out _, out _, name, size))
        return null;
      return new string(name);
    }

    private static string DescribeError(int error, string root) {
      switch (error) {
        case ERROR_JOURNAL_NOT_ACTIVE:
          return $"the change journal is not active on \"{root}\"";
        case ERROR_JOURNAL_DELETE_IN_PROGRESS:
          return $"the change journal of \"{root}\" is being deleted";
        case ERROR_JOURNAL_ENTRY_DELETED:
          return "the change journal wrapped (records after the checkpoint were overwritten)";
        case ERROR_INVALID_FUNCTION:
        case ERROR_NOT_SUPPORTED:
          return $"the volume of \"{root}\" has no change journal";
        default:
          return $"change journal error on \"{root}\": {new Win32Exception(error).Message}";
      }
    }
  }
}
