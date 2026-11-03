// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Implementation of <see cref="IFileSystem"/> for the server, with portable
  /// .NET APIs only (no Win32 API): <see cref="FileSystemEnumerable{TResult}"/>
  /// for directory enumeration, <see cref="File.OpenHandle"/> and <see
  /// cref="RandomAccess"/> for reading files, <see cref="FileSystemInfo"/> for
  /// file attributes. Replaces <c>VsChromium.Core.Files.FileSystem</c> (used by
  /// the Visual Studio side), which uses <c>NtQueryDirectoryFile</c>,
  /// <c>GetFileAttributesEx</c> and <c>ReadFile</c>.
  /// </summary>
  public class PortableFileSystem : IFileSystem, IFileContentsReader {
    private static readonly EnumerationOptions DirectoryEnumerationOptions = new EnumerationOptions {
      RecurseSubdirectories = false,
      // Report errors (e.g. access denied) to the caller, as the native
      // implementation did.
      IgnoreInaccessible = false,
      // Return all entries, including hidden and system files: filtering is
      // done by the server configuration (".ignore" files).
      AttributesToSkip = 0,
      ReturnSpecialDirectories = false,
      MatchType = MatchType.Simple,
      MatchCasing = MatchCasing.PlatformDefault,
    };

    public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      return new PortableFileInfoSnapshot(path);
    }

    public string ReadText(FullPath path) {
      return File.ReadAllText(path.Value);
    }

    public SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) {
      using (var fileHandle = OpenFileForReading(path)) {
        return ReadFileNulTerminated(path, fileHandle, fileSize, trailingByteCount);
      }
    }

    public SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, int trailingByteCount, out DateTime lastWriteTimeUtc) {
      using (var fileHandle = OpenFileForReading(path)) {
        // Note: Reading the attributes from the open handle avoids a second
        // lookup of the path in the file system.
        lastWriteTimeUtc = File.GetLastWriteTimeUtc(fileHandle);
        var fileSize = RandomAccess.GetLength(fileHandle);
        return ReadFileNulTerminated(path, fileHandle, fileSize, trailingByteCount);
      }
    }

    private static SafeFileHandle OpenFileForReading(FullPath path) {
      // Note: Files may be modified/deleted (e.g. by a build) while we read them.
      return File.OpenHandle(path.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
    }

    private static unsafe SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, SafeFileHandle fileHandle, long fileSize, int trailingByteCount) {
      // Note: We are limited to 2GB files by design.
      var maxLen = int.MaxValue - trailingByteCount;
      if (fileSize >= maxLen) {
        Logger.LogWarn("File too big, truncated to {0} bytes", maxLen);
      }
      var len = (int)Math.Min(maxLen, fileSize);
      var block = NativeHeap.Alloc(len + trailingByteCount);
      try {
        var buffer = new Span<byte>(block.Pointer.ToPointer(), len + trailingByteCount);
        var bytesRead = 0;
        while (bytesRead < len) {
          var count = RandomAccess.Read(fileHandle, buffer.Slice(bytesRead, len - bytesRead), bytesRead);
          if (count == 0)
            break;
          bytesRead += count;
        }

        if (bytesRead != len)
          throw new IOException(string.Format("File read operation didn't read the whole file \"{0}\"", path));

        buffer.Slice(len).Clear();
      }
      catch (Exception) {
        block.Dispose();
        throw;
      }
      return block;
    }
    public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
      // Note: Unlike the Win32 implementation of the Visual Studio side, entries with paths longer
      // than MAX_PATH are not skipped (see "Behavior changes" in PORTING_PRINCIPLES.md).
      return new FileSystemEnumerable<DirectoryEntry>(
          path.Value,
          (ref FileSystemEntry entry) => new DirectoryEntry(entry.FileName.ToString(), entry.Attributes),
          DirectoryEnumerationOptions)
        .ToList();
    }
    public IFileSystemWatcher CreateDirectoryWatcher(FullPath path) {
      return new FileSystemWatcherImpl(path);
    }

    /// <summary>
    /// Snapshot of the attributes of a file or directory, read when the
    /// instance is created.
    /// </summary>
    private sealed class PortableFileInfoSnapshot : IFileInfoSnapshot {
      private readonly FullPath _path;
      private readonly FileAttributes _attributes;
      private readonly DateTime _lastWriteTimeUtc;
      private readonly long _length;
      private readonly Exception _error;

      public PortableFileInfoSnapshot(FullPath path) {
        _path = path;
        try {
          // Note: FileInfo works for directories too (except for "Length").
          var info = new FileInfo(path.Value);
          var attributes = info.Attributes;
          if ((int)attributes == -1) {
            _error = new FileNotFoundException(string.Format("File \"{0}\" not found", path), path.Value);
            return;
          }
          _attributes = attributes;
          _lastWriteTimeUtc = info.LastWriteTimeUtc;
          _length = ((attributes & FileAttributes.Directory) == 0) ? info.Length : 0;
        }
        catch (Exception e) {
          _error = e;
        }
      }

      public FullPath Path { get { return _path; } }

      public bool Exists { get { return _error == null; } }

      public bool IsFile { get { return Exists && (_attributes & FileAttributes.Directory) == 0; } }

      public bool IsDirectory { get { return Exists && (_attributes & FileAttributes.Directory) != 0; } }

      public bool IsSymLink { get { return Exists && (_attributes & FileAttributes.ReparsePoint) != 0; } }

      public DateTime LastWriteTimeUtc {
        get {
          ThrowOnError();
          return _lastWriteTimeUtc;
        }
      }

      public long Length {
        get {
          ThrowOnError();
          return _length;
        }
      }

      private void ThrowOnError() {
        if (_error != null) {
          throw new Exception(string.Format("Error reading attributes of file \"{0}\"", _path), _error);
        }
      }
    }
  }
}
