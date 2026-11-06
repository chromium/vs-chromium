// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Files {
  public interface IFileSystem {
    /// <summary>
    /// Returns an instance of <see cref="IFileInfoSnapshot"/> of a file or
    /// directory located t <paramref name="path"/>. No exception is thrown
    /// until <see cref="IFileInfoSnapshot"/> member functions are called.
    /// </summary>
    IFileInfoSnapshot GetFileInfoSnapshot(FullPath path);

    /// <summary>
    /// Reads the full contents of a text file into a string.
    /// </summary>
    string ReadText(FullPath path);

    /// <summary>
    /// Reads the full contents of a file in memory, with <paramref
    /// name="trailingByteCount"/> null bytes as suffix.
    /// </summary>
    SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, long fileSize, int trailingByteCount);

    /// <summary>
    /// Return the list of directory entries contained in the directory
    /// <paramref name="path"/>.
    /// </summary>
    IList<DirectoryEntry> GetDirectoryEntries(FullPath path);

    /// <summary>
    /// Returns a new <see cref="IFileSystemWatcher"/> instances for the given directory path.
    /// </summary>
    IFileSystemWatcher CreateDirectoryWatcher(FullPath path);

    /// <summary>
    /// The volume serial number and the 128-bit file id of the directory <paramref name="path"/>,
    /// following symbolic links and junctions (the id of their target). False if unknown or not
    /// supported (see <see cref="Windows.WindowsFileSystem"/>).
    /// </summary>
    bool TryGetDirectoryTarget(FullPath path, out ulong volumeSerialNumber, out System.UInt128 fileId) {
      volumeSerialNumber = 0;
      fileId = default;
      return false;
    }
  }
}
