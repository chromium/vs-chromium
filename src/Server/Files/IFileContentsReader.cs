// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Optional interface of <see cref="IFileSystem"/> implementations (server
  /// only): reading the contents of a file and its attributes with a single
  /// lookup of the path in the file system.
  /// </summary>
  public interface IFileContentsReader {
    /// <summary>
    /// Reads the full contents of the file in memory, with <paramref
    /// name="trailingByteCount"/> null bytes as suffix, and returns the last
    /// write time of the file read from the open file.
    /// </summary>
    SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, int trailingByteCount, out DateTime lastWriteTimeUtc);
  }
}
