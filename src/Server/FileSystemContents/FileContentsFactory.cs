// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.FileSystemContents {
  public class FileContentsFactory : IFileContentsFactory {
    private const int TrailingByteCount = 2;
    private static readonly FileContentsPrefixPredicate IsBinaryPrefix = static prefix => TextKindDetector.IsBinaryPrefix(prefix);

    private readonly IFileSystem _fileSystem;

    public FileContentsFactory(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public FileContents ReadFileContents(FullPath path) {
      SafeNativeMemoryHandle block = null;
      try {
        DateTime lastWriteTimeUtc;
        if (_fileSystem is IFileContentsReader reader) {
          // Faster: a single path lookup (attributes are read from the open file), and binary
          // files are detected from their first bytes, without reading them entirely.
          block = reader.ReadFileNulTerminated(path, TrailingByteCount, TextKindDetector.BinaryDetectionPrefixLength,
            IsBinaryPrefix, out lastWriteTimeUtc, out var fileLength);
          if (block == null) {
            return new BinaryFileContents(lastWriteTimeUtc, fileLength);
          }
        } else {
          var fileInfo = _fileSystem.GetFileInfoSnapshot(path);
          block = _fileSystem.ReadFileNulTerminated(fileInfo.Path, fileInfo.Length, TrailingByteCount);
          lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        }
        var result = ReadFileContentsWorker(block, lastWriteTimeUtc);
        block = null; // Owned by "result" (or released)
        return result;
      }
      catch (Exception e) {
        // Note: Release the native memory now (up to 2 GB per file) rather than when the handle
        // is finalized.
        block?.Dispose();
        Logger.LogWarn(e, "Skipping file \"{0}\" because of an error reading its contents", path);
        return BinaryFileContents.Empty;
      }
    }

    /// <summary>
    /// Creates the file contents of <paramref name="block"/> (the bytes of the file followed by
    /// <see cref="TrailingByteCount"/> null bytes): the block is owned by the result, or released.
    /// </summary>
    private static unsafe FileContents ReadFileContentsWorker(SafeNativeMemoryHandle block, DateTime lastWriteTimeUtc) {
      var contentsByteCount = block.ByteLength - TrailingByteCount; // Padding added by ReadFileNulTerminated
      var bytes = new ReadOnlySpan<byte>(block.Pointer.ToPointer(), contentsByteCount);
      var kind = TextKindDetector.GetKind(bytes);
      var utf8BomSize = TextKindDetector.Utf8BomSize;

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

        // UTF-16 files are converted to UTF-8 (same storage and search as the other text files).
        // Positions of search results are UTF-16 positions in the decoded text, i.e. the
        // positions in the text the editors display.
        case TextKind.Utf16LittleEndianWithBom:
        case TextKind.Utf16BigEndianWithBom: {
          var encoding = kind == TextKind.Utf16LittleEndianWithBom ? Encoding.Unicode : Encoding.BigEndianUnicode;
          var utf16Bytes = bytes.Slice(TextKindDetector.Utf16BomSize);
          var result = CreateFromUtf16(utf16Bytes, encoding, lastWriteTimeUtc);
          block.Dispose();
          return result;
        }

        case TextKind.Binary:
          block.Dispose();
          return new BinaryFileContents(lastWriteTimeUtc, contentsByteCount);

        default:
          Invariants.Assert(false);
          throw new InvalidOperationException();
      }
    }

    private static unsafe FileContents CreateFromUtf16(ReadOnlySpan<byte> utf16Bytes, Encoding encoding, DateTime lastWriteTimeUtc) {
      // Note: Invalid sequences (lone surrogates, odd number of bytes) are decoded as U+FFFD, as
      // the editors do.
      var text = encoding.GetString(utf16Bytes);
      var utf8Length = Encoding.UTF8.GetByteCount(text);
      var utf8Block = NativeHeap.Alloc(utf8Length + TrailingByteCount);
      try {
        var utf8 = new Span<byte>(utf8Block.Pointer.ToPointer(), utf8Length + TrailingByteCount);
        Encoding.UTF8.GetBytes(text, utf8);
        utf8.Slice(utf8Length).Clear();
        var isAscii = Ascii.IsValid(utf8.Slice(0, utf8Length));
        return new Utf8FileContents(new FileContentsMemory(utf8Block, 0, utf8Length), lastWriteTimeUtc, isAscii);
      }
      catch (Exception) {
        utf8Block.Dispose();
        throw;
      }
    }
  }
}
