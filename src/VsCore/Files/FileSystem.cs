// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;
using NativeMethods = VsChromium.Core.Win32.Files.NativeMethods;

namespace VsChromium.Core.Files {
  [Export(typeof(IFileSystem))]
  public class FileSystem : IFileSystem {
    private const long MaxPathTooLogLogCount = 10;
    private long _pathTooLongErrorCount;

    public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      WIN32_FILE_ATTRIBUTE_DATA data;
      int win32Error;
      ReadFileAttribues(path, out data, out win32Error);
      return new FileInfoSnapshot(new SlimFileInfo(path, data, win32Error));
    }

    private static void ReadFileAttribues(FullPath path, out WIN32_FILE_ATTRIBUTE_DATA data, out int win32Error) {
      win32Error = 0;
      data = default(WIN32_FILE_ATTRIBUTE_DATA);
      if (!NativeMethods.GetFileAttributesEx(path.Value, 0, ref data))
        win32Error = Marshal.GetLastWin32Error();
    }


    public bool FileExists(FullPath path) {
      return File.Exists(path.Value);
    }

    public bool DirectoryExists(FullPath path) {
      return Directory.Exists(path.Value);
    }

    public DateTime GetFileLastWriteTimeUtc(FullPath path) {
      return File.GetLastWriteTimeUtc(path.Value);
    }

    public string ReadText(FullPath path) {
      return File.ReadAllText(path.Value);
    }

    public SafeHeapBlockHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount) {
      return NativeFile.ReadFileNulTerminated(path, fileSize, trailingByteCount);
    }

    public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
      var list = NativeFile.GetDirectoryEntries(path.Value);
      // Skip any entry that is longer than MAX_PATH.
      // Fix this once we fully support the long path syntax ("\\?\" prefix)
      if (list.Any(entry => PathHelpers.IsPathTooLong(path.Value, entry.Name))) {
        return list.Where(entry => {
          if (PathHelpers.IsPathTooLong(path.Value, entry.Name)) {
            // Note: The condition is unsafe from a pure concurrency point of view,
            //       but is ok in this case because the field is incrementally increasing.
            //       This is just an optimization to avoid an Interlocked call.
            if (_pathTooLongErrorCount <= MaxPathTooLogLogCount) {
              var logCount = Interlocked.Increment(ref _pathTooLongErrorCount);
              if (logCount <= MaxPathTooLogLogCount) {
                Logger.LogInfo("Skipping directory entry because path is too long: \"{0}\"",
                  path.Combine(new RelativePath(entry.Name)));
              }
              if (logCount == MaxPathTooLogLogCount) {
                Logger.LogInfo("  (Note: No more message abount path too long will be logged, because the maximum number of occurrences has been reached)");
              }
            }
            return false;
          }
          return true;
        }).ToList();
      }
      return list;
    }

    public IFileSystemWatcher CreateDirectoryWatcher(FullPath path) {
      return new FileSystemWatcherImpl(path);
    }
  }
}
