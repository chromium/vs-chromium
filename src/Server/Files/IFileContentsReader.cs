// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Decides, from the first bytes of a file, that its contents don't need to be read (e.g. a
  /// binary file).
  /// </summary>
  public delegate bool FileContentsPrefixPredicate(ReadOnlySpan<byte> prefix);

  /// <summary>
  /// Optional interface of <see cref="IFileSystem"/> implementations (server
  /// only): reading the contents of a file and its attributes with a single
  /// lookup of the path in the file system.
  /// </summary>
  public interface IFileContentsReader {
    /// <summary>
    /// Reads the full contents of the file in memory, with <paramref
    /// name="trailingByteCount"/> null bytes as suffix, and returns the last
    /// write time and the length of the file read from the open file.
    /// <para>If <paramref name="skipContents"/> is not null, it is first called with the first
    /// <paramref name="prefixLength"/> bytes of the file (or the whole file if shorter): if it
    /// returns true, the rest of the file is not read and the method returns null.</para>
    /// </summary>
    SafeNativeMemoryHandle ReadFileNulTerminated(FullPath path, int trailingByteCount, int prefixLength,
      FileContentsPrefixPredicate skipContents, out DateTime lastWriteTimeUtc, out long fileLength);
  }
}
