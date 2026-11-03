// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.FileSystemContents {
  public class FileContentsFactory : IFileContentsFactory {
    private readonly IFileSystem _fileSystem;

    public FileContentsFactory(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public FileContents ReadFileContents(FullPath path) {
      SafeNativeMemoryHandle block = null;
      try {
        const int trailingByteCount = 2;
        DateTime lastWriteTimeUtc;
        if (_fileSystem is IFileContentsReader reader) {
          // Faster: a single path lookup (attributes are read from the open file).
          block = reader.ReadFileNulTerminated(path, trailingByteCount, out lastWriteTimeUtc);
        } else {
          var fileInfo = _fileSystem.GetFileInfoSnapshot(path);
          block = _fileSystem.ReadFileNulTerminated(fileInfo.Path, fileInfo.Length, trailingByteCount);
          lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        }
        return ReadFileContentsWorker(block, trailingByteCount, lastWriteTimeUtc);
      }
      catch (Exception e) {
        // Note: Release the native memory now (up to 2 GB per file) rather than when the handle
        // is finalized.
        block?.Dispose();
        Logger.LogWarn(e, "Skipping file \"{0}\" because of an error reading its contents", path);
        return BinaryFileContents.Empty;
      }
    }

    private FileContents ReadFileContentsWorker(SafeNativeMemoryHandle block, int trailingByteCount, DateTime lastWriteTimeUtc) {
      var contentsByteCount = block.ByteLength - trailingByteCount; // Padding added by ReadFileNulTerminated
      var kind = GetTextKind(block, contentsByteCount);
      const int utf8BomSize = 3;

      switch (kind) {
        case TextKind.Ascii:
          return new Utf8FileContents(new FileContentsMemory(block, 0, contentsByteCount), lastWriteTimeUtc, isAscii: true);

        case TextKind.AsciiWithUtf8Bom:
          return new Utf8FileContents(new FileContentsMemory(block, utf8BomSize, contentsByteCount - utf8BomSize), lastWriteTimeUtc, isAscii: true);

        // Files with non ASCII characters are stored as UTF-8 too (half the
        // memory of UTF-16 for mostly ASCII text). Positions of search results
        // are converted to UTF-16 positions (see Utf8FileContents).
        case TextKind.Utf8:
          return new Utf8FileContents(new FileContentsMemory(block, 0, contentsByteCount), lastWriteTimeUtc, isAscii: false);

        case TextKind.Utf8WithBom:
          return new Utf8FileContents(new FileContentsMemory(block, utf8BomSize, contentsByteCount - utf8BomSize), lastWriteTimeUtc, isAscii: false);

        case TextKind.ProbablyBinary:
          block.Dispose();
          return new BinaryFileContents(lastWriteTimeUtc, contentsByteCount);

        default:
          Invariants.Assert(false);
          throw new InvalidOperationException();
      }
    }

    private static unsafe TextKind GetTextKind(SafeNativeMemoryHandle block, int contentsByteCount) {
      return TextKindDetector.GetKind(new ReadOnlySpan<byte>(block.Pointer.ToPointer(), contentsByteCount));
    }
  }
}
