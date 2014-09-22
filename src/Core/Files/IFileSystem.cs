// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
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

    void GetDirectoryEntries(FullPath path, out IList<string> directories, out IList<string> files);
  }
}
