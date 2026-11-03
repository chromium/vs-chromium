// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Memory;

namespace VsChromium.Core.Files {
  /// <summary>
  /// Allocates the memory of the contents of a file of <paramref name="size"/> bytes (contents and
  /// trailing bytes), after examining its first bytes (<paramref name="prefix"/>). Returns a null
  /// slice if the contents don't need to be read (e.g. a binary file).
  /// </summary>
  public delegate NativeMemorySlice FileContentsAllocator(ReadOnlySpan<byte> prefix, int size);

  /// <summary>
  /// Optional interface of <see cref="IFileSystem"/> implementations (server
  /// only): reading the contents of a file and its attributes with a single
  /// lookup of the path in the file system.
  /// </summary>
  public interface IFileContentsReader {
    /// <summary>
    /// Reads the full contents of the file in memory allocated by <paramref name="allocate"/>,
    /// with <paramref name="trailingByteCount"/> null bytes as suffix, and returns the last write
    /// time and the length of the file read from the open file.
    /// <para><paramref name="allocate"/> is called with the first <paramref name="prefixLength"/>
    /// bytes of the file (or the whole file if shorter), before any memory is allocated for the
    /// contents: if it returns a null slice, the rest of the file is not read and the method
    /// returns a null slice.</para>
    /// </summary>
    NativeMemorySlice ReadFileContents(FullPath path, int trailingByteCount, int prefixLength,
      FileContentsAllocator allocate, out DateTime lastWriteTimeUtc, out long fileLength);
  }
}
