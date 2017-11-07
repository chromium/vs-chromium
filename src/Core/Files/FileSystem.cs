// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Files {
  [Export(typeof(IFileSystem))]
  public class FileSystem : IFileSystem {
    private const long MAX_PATH_TOO_LOG_LOG_COUNT = 10;
    private long _pathTooLongErrorCount;

    public IFileInfoSnapshot GetFileInfoSnapshot(FullPath path) {
      return new FileInfoSnapshot(path);
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

    public IList<string> ReadAllLines(FullPath path) {
      return File.ReadAllLines(path.Value);
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
            if (_pathTooLongErrorCount <= MAX_PATH_TOO_LOG_LOG_COUNT) {
              var logCount = Interlocked.Increment(ref _pathTooLongErrorCount);
              if (logCount <= MAX_PATH_TOO_LOG_LOG_COUNT) {
                Logger.LogInfo("Skipping directory entry because path is too long: \"{0}\"",
                  path.Combine(new RelativePath(entry.Name)));
              }
              if (logCount == MAX_PATH_TOO_LOG_LOG_COUNT) {
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
  }
}
