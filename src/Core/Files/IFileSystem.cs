// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Win32.Files;
using VsChromium.Core.Win32.Memory;

namespace VsChromium.Core.Files {
  public interface IFileSystem {
    /// <summary>
    /// Returns an instance of <see cref="IFileInfoSnapshot"/> of a file or
    /// directory located t <paramref name="path"/>. No exception is thrown
    /// until <see cref="IFileInfoSnapshot"/> member functions are called.
    /// </summary>
    IFileInfoSnapshot GetFileInfoSnapshot(FullPath path);

    /// <summary>
    /// Reads the full contents of a text file into a list of strings.
    /// </summary>
    IList<string> ReadAllLines(FullPath path);

    /// <summary>
    /// Reads the full contents of a file in memory, with <paramref
    /// name="trailingByteCount"/> null bytes as suffix.
    /// </summary>
    SafeHeapBlockHandle ReadFileNulTerminated(IFileInfoSnapshot fileInfo, int trailingByteCount);

    /// <summary>
    /// Return the list of directory entries contained in the directory
    /// <paramref name="path"/>.
    /// </summary>
    IList<DirectoryEntry> GetDirectoryEntries(FullPath path);
  }
}
