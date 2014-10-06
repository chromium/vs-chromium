// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Files {
  public interface IFileSystem {
    /// <summary>
    /// Returns attributes of a file or directory in the file system.
    /// </summary>
    IFileInfoSnapshot GetFileInfoSnapshot(FullPath path);

    /// <summary>
    /// Reads the full contents of a text file into a list of strings.
    /// </summary>
    string[] ReadAllLines(FullPath path);

    /// <summary>
    /// Reads the full contents of a file in memory, with <paramref
    /// name="trailingByteCount"/> null bytes as suffix.
    /// </summary>
    SafeHeapBlockHandle ReadFileNulTerminated(IFileInfoSnapshot fileInfo, int trailingByteCount);

    DirectoryEntries GetDirectoryEntries(FullPath path, GetDirectoryEntriesOptions options = GetDirectoryEntriesOptions.Default);
  }

  [Flags]
  public enum GetDirectoryEntriesOptions {
    Default = 0x0000,
    FollowSymlinks = 0x0001,
  }

  public struct DirectoryEntries {
    private readonly IList<DirectoryEntry> _directories;
    private readonly IList<DirectoryEntry> _files;

    public DirectoryEntries(IList<DirectoryEntry> directories, IList<DirectoryEntry> files)
      : this() {
      _directories = directories;
      _files = files;
    }

    public IList<DirectoryEntry> Directories {
      get { return _directories; }
    }

    public IList<DirectoryEntry> Files {
      get { return _files; }
    }
  }
}
