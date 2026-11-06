// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32.SafeHandles;
using static VsChromium.Core.Files.Windows.WindowsNativeMethods;

namespace VsChromium.Core.Files.Windows {
  /// <summary>
  /// Implementation of <see cref="IFileSystem"/> for Windows (selected at runtime, see
  /// <c>ServerComponents</c>), an exception to the "portable .NET only" rule (see "Windows specific
  /// code" in PORTING_PRINCIPLES.md): the same as <see cref="PortableFileSystem"/>, except that
  /// directories are enumerated with <c>NtQueryDirectoryFile</c> and
  /// <c>FileIdExtdDirectoryInformation</c>, which returns the 128-bit file id of each entry (used
  /// to map the records of the change journal to directories, see <c>ChangeJournalCatchUp</c>),
  /// with a 64 KB buffer instead of the 4 KB of <see cref="System.IO.Enumeration.FileSystemEnumerable{TResult}"/>.
  /// The file systems that don't support this information class (e.g. FAT, some network file
  /// systems) are enumerated by <see cref="PortableFileSystem"/> (no file ids).
  /// </summary>
  [SupportedOSPlatform("windows")]
  public sealed class WindowsFileSystem : PortableFileSystem, IFileSystem {
    private const int BufferSize = 64 * 1024;

    /// <summary>The buffer of the enumeration of each thread (pinned: no copy).</summary>
    [ThreadStatic]
    private static byte[] t_buffer;

    public bool TryGetDirectoryTarget(FullPath path, out ulong volumeSerialNumber, out UInt128 fileId) {
      try {
        // Note: Follows symbolic links and junctions (no FILE_FLAG_OPEN_REPARSE_POINT).
        using (var handle = OpenHandle(path.Value, FILE_READ_ATTRIBUTES)) {
          var result = TryGetFileId(handle, out volumeSerialNumber, out fileId);
          RecordFinalPath(path.Value, handle);
          return result;
        }
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        volumeSerialNumber = 0;
        fileId = default;
        return false;
      }
    }

    public override IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      return new WindowsFileInfoSnapshot(base.GetFileInfoSnapshot(path));
    }

    public override IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
      var result = TryGetDirectoryEntries(path.Value);
      return result ?? base.GetDirectoryEntries(path);
    }

    /// <summary>
    /// The entries of a directory, or null if its file system doesn't support
    /// <c>FileIdExtdDirectoryInformation</c>.
    /// </summary>
    private static unsafe List<DirectoryEntry> TryGetDirectoryEntries(string path) {
      using (var handle = OpenHandle(path, FILE_LIST_DIRECTORY)) {
        var buffer = t_buffer ??= GC.AllocateUninitializedArray<byte>(BufferSize, pinned: true);
        var result = new List<DirectoryEntry>();
        fixed (byte* bufferStart = buffer) {
          for (var first = true; ; first = false) {
            var status = NtQueryDirectoryFile(handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, out _, bufferStart, BufferSize,
              FileIdExtdDirectoryInformation, 0, IntPtr.Zero, (byte)(first ? 1 : 0));
            if (status == STATUS_NO_MORE_FILES || status == STATUS_NO_SUCH_FILE)
              break;
            if (first && (status == STATUS_INVALID_INFO_CLASS || status == STATUS_NOT_SUPPORTED || status == STATUS_INVALID_PARAMETER))
              return null;
            if (status != STATUS_SUCCESS)
              throw CreateException((int)RtlNtStatusToDosError(status), path);
            ParseEntries(path, bufferStart, result);
          }
        }
        return result;
      }
    }

    // Offsets of the fields of FILE_ID_EXTD_DIR_INFORMATION.
    private const int NextEntryOffsetOffset = 0;
    private const int LastWriteTimeOffset = 24;
    private const int EndOfFileOffset = 40;
    private const int FileAttributesOffset = 56;
    private const int FileNameLengthOffset = 60;
    private const int ReparsePointTagOffset = 68;
    private const int FileIdOffset = 72;
    private const int FileNameOffset = 88;

    private static unsafe void ParseEntries(string directory, byte* buffer, List<DirectoryEntry> result) {
      var entry = buffer;
      while (true) {
        var nameLength = *(int*)(entry + FileNameLengthOffset) / sizeof(char);
        var name = new ReadOnlySpan<char>(entry + FileNameOffset, nameLength);
        if (!IsSpecialDirectoryName(name)) {
          var attributes = (FileAttributes)(*(uint*)(entry + FileAttributesOffset));
          var lastWriteTime = *(long*)(entry + LastWriteTimeOffset);
          var length = *(long*)(entry + EndOfFileOffset);
          var fileId = new UInt128(*(ulong*)(entry + FileIdOffset + 8), *(ulong*)(entry + FileIdOffset));
          var nameString = name.ToString();
          var metadata = new FileMetadata(DateTime.FromFileTimeUtc(Math.Max(0, lastWriteTime)), length);
          // Note: Same as PortableFileSystem: the metadata of the target of file symbolic links
          // (only symbolic links: other reparse points, e.g. cloud files, are not opened), with a
          // single open of the target.
          if (IsFileReparsePoint(attributes) && *(uint*)(entry + ReparsePointTagOffset) == IO_REPARSE_TAG_SYMLINK &&
              TryGetMetadata(Path.Join(directory, nameString), followLink: true, out var targetMetadata, out var isDirectory) &&
              !isDirectory) {
            metadata = targetMetadata;
          }
          result.Add(new DirectoryEntry(nameString, attributes, metadata, fileId));
        }
        var next = *(int*)(entry + NextEntryOffsetOffset);
        if (next == 0)
          break;
        entry += next;
      }
    }

    public FileMetadata? GetSymLinkFileMetadata(FullPath path) {
      if (TryGetMetadata(path.Value, followLink: true, out var metadata, out var isDirectory) && !isDirectory)
        return metadata;
      // Dangling link (or to a directory): the metadata of the link itself, as in the enumeration.
      if (TryGetMetadata(path.Value, followLink: false, out metadata, out isDirectory) && !isDirectory)
        return metadata;
      return null;
    }

    /// <summary>
    /// The last write time and size of a file (of the final target of a symbolic link if <paramref
    /// name="followLink"/>), with a single open (FILE_READ_ATTRIBUTES).
    /// </summary>
    private static unsafe bool TryGetMetadata(string path, bool followLink, out FileMetadata metadata, out bool isDirectory) {
      metadata = default;
      isDirectory = false;
      var handle = CreateFile(ToNativePath(path), FILE_READ_ATTRIBUTES, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS | (followLink ? 0 : FILE_FLAG_OPEN_REPARSE_POINT), IntPtr.Zero);
      using (handle) {
        if (handle.IsInvalid)
          return false;
        FILE_BASIC_INFO basic;
        FILE_STANDARD_INFO standard;
        if (!GetFileInformationByHandleEx(handle, FileBasicInfo, &basic, (uint)sizeof(FILE_BASIC_INFO)) ||
            !GetFileInformationByHandleEx(handle, FileStandardInfo, &standard, (uint)sizeof(FILE_STANDARD_INFO)))
          return false;
        isDirectory = standard.Directory != 0;
        metadata = new FileMetadata(DateTime.FromFileTimeUtc(Math.Max(0, basic.LastWriteTime)), standard.EndOfFile);
        if (followLink) {
          RecordFinalPath(path, handle);
        }
        return true;
      }
    }

    /// <summary>
    /// The final targets of the symbolic links resolved by the scans (link path => final path),
    /// reused by <see cref="ResolveSymLinkTarget"/> (no other open of the link).
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> SymLinkTargets = new ConcurrentDictionary<string, string>(PathHelpers.FileNameComparer);

    private static void RecordFinalPath(string path, SafeFileHandle handle) {
      var target = GetFinalPath(handle);
      if (target != null) {
        SymLinkTargets[path] = target;
      }
    }

    public string ResolveSymLinkTarget(FullPath path, bool allowCached) {
      if (allowCached && SymLinkTargets.TryGetValue(path.Value, out var cached))
        return cached;
      var handle = CreateFile(ToNativePath(path.Value), FILE_READ_ATTRIBUTES, FILE_SHARE_ALL, IntPtr.Zero, OPEN_EXISTING,
        FILE_FLAG_BACKUP_SEMANTICS, IntPtr.Zero);
      using (handle) {
        if (handle.IsInvalid) {
          // E.g. dangling link.
          SymLinkTargets.TryRemove(path.Value, out _);
          return null;
        }
        var target = GetFinalPath(handle);
        if (target == null) {
          SymLinkTargets.TryRemove(path.Value, out _);
        } else {
          SymLinkTargets[path.Value] = target;
        }
        return target;
      }
    }

    private static bool IsSpecialDirectoryName(ReadOnlySpan<char> name) {
      return (name.Length == 1 && name[0] == '.') || (name.Length == 2 && name[0] == '.' && name[1] == '.');
    }

    /// <summary>
    /// The 128-bit id of a file or directory (of the reparse point itself for symbolic links and
    /// junctions, as in the enumeration of its parent), 0 if it can't be read.
    /// </summary>
    public static UInt128 GetFileId(string path) {
      try {
        using (var handle = OpenHandle(path, FILE_READ_ATTRIBUTES, openReparsePoint: true)) {
          return TryGetFileId(handle, out _, out var fileId) ? fileId : default;
        }
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        return default;
      }
    }

    /// <summary>
    /// <see cref="PortableFileSystem"/> snapshot, with the file id (read when first used: it opens
    /// the file).
    /// </summary>
    private sealed class WindowsFileInfoSnapshot : IFileInfoSnapshot {
      private readonly IFileInfoSnapshot _snapshot;
      private UInt128? _fileId;

      public WindowsFileInfoSnapshot(IFileInfoSnapshot snapshot) {
        _snapshot = snapshot;
      }

      public FullPath Path => _snapshot.Path;
      public bool Exists => _snapshot.Exists;
      public bool IsFile => _snapshot.IsFile;
      public bool IsDirectory => _snapshot.IsDirectory;
      public bool IsSymLink => _snapshot.IsSymLink;
      public DateTime LastWriteTimeUtc => _snapshot.LastWriteTimeUtc;
      public long Length => _snapshot.Length;

      public UInt128 FileId {
        get {
          if (!_fileId.HasValue) {
            _fileId = Exists ? GetFileId(Path.Value) : default;
          }
          return _fileId.Value;
        }
      }
    }
  }
}
