// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using System.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.FileSystemContents {
  public class FileContentsFactory : IFileContentsFactory {
    private const int TrailingByteCount = 2;

    private readonly IFileSystem _fileSystem;
    private readonly ArenaAllocator _arenaAllocator;
    private readonly FileContentsAllocator _allocateContents;

    public FileContentsFactory(IFileSystem fileSystem) : this(fileSystem, new ArenaAllocator()) {
    }

    public FileContentsFactory(IFileSystem fileSystem, ArenaAllocator arenaAllocator) {
      _fileSystem = fileSystem;
      _arenaAllocator = arenaAllocator;
      _allocateContents = AllocateContents;
    }

    /// <summary>
    /// The memory of the contents of a file, decided from its first bytes: none for binary files
    /// (not read), a temporary block for UTF-16 files (converted to UTF-8, see
    /// <see cref="CreateFromUtf16"/>), a slot of an arena for the other files (the text is stored
    /// as is).
    /// </summary>
    private NativeMemorySlice AllocateContents(ReadOnlySpan<byte> prefix, int size) {
      if (TextKindDetector.IsBinaryPrefix(prefix)) {
        return default;
      }
      if (TextKindDetector.HasUtf16Bom(prefix)) {
        return new NativeMemorySlice(NativeHeap.Alloc(size), 0, size, isDedicated: true);
      }
      return _arenaAllocator.Allocate(size);
    }

    /// <summary>
    /// The block and the size of the arena slot of <paramref name="contents"/>, if allocated in an
    /// arena: the text, preceded by the UTF-8 BOM if any (slots start on aligned offsets, so the
    /// BOM is the remainder of the offset) and followed by the trailing bytes.
    /// </summary>
    public static (SafeHandle Block, int SlotSize) GetArenaSlot(FileContents contents) {
      var memory = contents.Contents;
      var bomSize = memory.Offset % ArenaAllocator.Alignment;
      return (memory.Block, ArenaAllocator.GetSlotSize(bomSize + memory.ByteLength + TrailingByteCount));
    }

    public FileContents ReadFileContents(FullPath path) {
      var slice = default(NativeMemorySlice);
      try {
        DateTime lastWriteTimeUtc;
        if (_fileSystem is IFileContentsReader reader) {
          // Faster: a single path lookup (attributes are read from the open file), and binary
          // files are detected from their first bytes, without reading them entirely.
          slice = reader.ReadFileContents(path, TrailingByteCount, TextKindDetector.BinaryDetectionPrefixLength,
            _allocateContents, out lastWriteTimeUtc, out var fileLength);
          if (slice.IsNull) {
            return new BinaryFileContents(lastWriteTimeUtc, fileLength);
          }
        } else {
          var fileInfo = _fileSystem.GetFileInfoSnapshot(path);
          var block = _fileSystem.ReadFileNulTerminated(fileInfo.Path, fileInfo.Length, TrailingByteCount);
          slice = new NativeMemorySlice(block, 0, block.ByteLength, isDedicated: true);
          lastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
        }
        var result = ReadFileContentsWorker(slice, lastWriteTimeUtc);
        slice = default; // Owned by "result" (or released)
        return result;
      }
      catch (Exception e) {
        // Note: Release the native memory now (up to 2 GB per file) rather than when the handle
        // is finalized (dedicated blocks only: the slot of an arena stays unused until the arena
        // is released).
        slice.Release();
        Logger.LogWarn(e, "Skipping file \"{0}\" because of an error reading its contents", path);
        return BinaryFileContents.Empty;
      }
    }

    /// <summary>
    /// Creates the file contents of <paramref name="slice"/> (the bytes of the file followed by
    /// <see cref="TrailingByteCount"/> null bytes): the memory is owned by the result, or released.
    /// </summary>
    private FileContents ReadFileContentsWorker(NativeMemorySlice slice, DateTime lastWriteTimeUtc) {
      var contentsByteCount = slice.Length - TrailingByteCount; // Padding added by ReadFileNulTerminated
      var bytes = slice.Span.Slice(0, contentsByteCount);
      var kind = TextKindDetector.GetKind(bytes);
      var utf8BomSize = TextKindDetector.Utf8BomSize;

      switch (kind) {
        case TextKind.Ascii:
          return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset, contentsByteCount), lastWriteTimeUtc, isAscii: true);

        case TextKind.AsciiWithUtf8Bom:
          return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset + utf8BomSize, contentsByteCount - utf8BomSize), lastWriteTimeUtc, isAscii: true);

        // Files with non ASCII characters are stored as UTF-8 too (half the
        // memory of UTF-16 for mostly ASCII text). Positions of search results
        // are converted to UTF-16 positions (see Utf8FileContents).
        case TextKind.Utf8:
          return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset, contentsByteCount), lastWriteTimeUtc, isAscii: false);

        case TextKind.Utf8WithBom:
          return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset + utf8BomSize, contentsByteCount - utf8BomSize), lastWriteTimeUtc, isAscii: false);

        // UTF-16 files are converted to UTF-8 (same storage and search as the other text files).
        // Positions of search results are UTF-16 positions in the decoded text, i.e. the
        // positions in the text the editors display.
        case TextKind.Utf16LittleEndianWithBom:
        case TextKind.Utf16BigEndianWithBom: {
          var encoding = kind == TextKind.Utf16LittleEndianWithBom ? Encoding.Unicode : Encoding.BigEndianUnicode;
          var result = CreateFromUtf16(bytes.Slice(TextKindDetector.Utf16BomSize), encoding, lastWriteTimeUtc);
          slice.Release();
          return result;
        }

        case TextKind.Binary:
          slice.Release();
          return new BinaryFileContents(lastWriteTimeUtc, contentsByteCount);

        default:
          Invariants.Assert(false);
          throw new InvalidOperationException();
      }
    }

    private FileContents CreateFromUtf16(ReadOnlySpan<byte> utf16Bytes, Encoding encoding, DateTime lastWriteTimeUtc) {
      // Note: Invalid sequences (lone surrogates, odd number of bytes) are decoded as U+FFFD, as
      // the editors do.
      var text = encoding.GetString(utf16Bytes);
      var utf8Length = Encoding.UTF8.GetByteCount(text);
      var utf8Slice = _arenaAllocator.Allocate(utf8Length + TrailingByteCount);
      try {
        var utf8 = utf8Slice.Span;
        Encoding.UTF8.GetBytes(text, utf8);
        utf8.Slice(utf8Length).Clear();
        var isAscii = Ascii.IsValid(utf8.Slice(0, utf8Length));
        return new Utf8FileContents(new FileContentsMemory(utf8Slice.Block, utf8Slice.Offset, utf8Length), lastWriteTimeUtc, isAscii);
      }
      catch (Exception) {
        utf8Slice.Release();
        throw;
      }
    }
  }
}
