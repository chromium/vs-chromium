// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Core.Win32;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.FileSystemContents {
  [Export(typeof(IFileContentsFactory))]
  public class FileContentsFactory : IFileContentsFactory {
    private readonly IFileSystem _fileSystem;

    [ImportingConstructor]
    public FileContentsFactory(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public FileContents ReadFileContents(FullPath path) {
      try {
        const int trailingByteCount = 2;
        SafeHeapBlockHandle block;
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
      catch (Win32Exception e) {
        Logger.LogWarn("Skipping file \"{0}\": {1} ({2})", path, e.Message, e.NativeErrorCode);
        return BinaryFileContents.Empty;
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Skipping file \"{0}\" because of an error reading its contents", path);
        return BinaryFileContents.Empty;
      }
    }

    private FileContents ReadFileContentsWorker(SafeHeapBlockHandle block, int trailingByteCount, DateTime lastWriteTimeUtc) {
      var contentsByteCount = block.ByteLength - trailingByteCount; // Padding added by ReadFileNulTerminated
      var kind = GetTextKind(block, contentsByteCount);
      const int utf8BomSize = 3;

      switch (kind) {
        case TextKind.Ascii:
          return new AsciiFileContents(new FileContentsMemory(block, 0, contentsByteCount), lastWriteTimeUtc);

        case TextKind.AsciiWithUtf8Bom:
          return new AsciiFileContents(new FileContentsMemory(block, utf8BomSize, contentsByteCount - utf8BomSize), lastWriteTimeUtc);

        // Files with non ASCII characters are converted to UTF-16, so that
        // positions of search results are character positions (not byte
        // positions), and so that searches (including case insensitive
        // searches and regular expressions) work on characters.
        case TextKind.Utf8:
          return CreateUtf16FileContents(block, 0, contentsByteCount, lastWriteTimeUtc);

        case TextKind.Utf8WithBom:
          return CreateUtf16FileContents(block, utf8BomSize, contentsByteCount - utf8BomSize, lastWriteTimeUtc);

        case TextKind.ProbablyBinary:
          block.Dispose();
          return new BinaryFileContents(lastWriteTimeUtc, contentsByteCount);

        default:
          Invariants.Assert(false);
          throw new InvalidOperationException();
      }
    }

    private static unsafe FileContents CreateUtf16FileContents(SafeHeapBlockHandle block, int offset, int length, DateTime lastWriteTimeUtc) {
      try {
        var bytes = new ReadOnlySpan<byte>(Pointers.AddPtr(block.Pointer, offset).ToPointer(), length);
        var charCount = Encoding.UTF8.GetCharCount(bytes);
        var utf16Block = HeapAllocStatic.Alloc(Math.Max(1, charCount) * sizeof(char));
        var chars = new Span<char>(utf16Block.Pointer.ToPointer(), charCount);
        Encoding.UTF8.GetChars(bytes, chars);
        return new Utf16FileContents(new FileContentsMemory(utf16Block, 0, charCount * sizeof(char)), lastWriteTimeUtc);
      }
      finally {
        block.Dispose();
      }
    }

    private static unsafe TextKind GetTextKind(SafeHeapBlockHandle block, int contentsByteCount) {
      return TextKindDetector.GetKind(new ReadOnlySpan<byte>(block.Pointer.ToPointer(), contentsByteCount));
    }
  }
}
