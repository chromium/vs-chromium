// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Files {
  [Export(typeof(IFileSystem))]
  public class FileSystem : IFileSystem {
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

    public SafeHeapBlockHandle ReadFileNulTerminated(IFileInfoSnapshot fileInfo, int trailingByteCount) {
      return NativeFile.ReadFileNulTerminated(((FileInfoSnapshot)fileInfo).SlimFileInfo, trailingByteCount);
    }

    public IList<DirectoryEntry> GetDirectoryEntries(FullPath path) {
      return NativeFile.GetDirectoryEntries(path.Value);
    }
  }
}
