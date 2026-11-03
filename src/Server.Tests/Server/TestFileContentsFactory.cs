// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.Search;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsFactory {
    /// <summary>
    /// Regression test: the native memory of a file whose contents failed to be processed (after
    /// reading) was only released by the finalizer of its handle (up to 2 GB per file).
    /// </summary>
    [TestMethod]
    public void NativeMemoryIsReleasedWhenProcessingTheContentsFails() {
      var fileSystem = new InvalidBlockFileSystem();
      var contents = new FileContentsFactory(fileSystem).ReadFileContents(new FullPath(@"C:\foo.txt"));

      Assert.AreSame(BinaryFileContents.Empty, contents);
      Assert.IsNotNull(fileSystem.LastBlock);
      Assert.IsTrue(fileSystem.LastBlock.IsClosed, "The native memory block was not released");
    }

    [TestMethod]
    public void Utf16FilesWithBomAreSearchableText() {
      foreach (var encoding in new[] { new UnicodeEncoding(bigEndian: false, byteOrderMark: true), new UnicodeEncoding(bigEndian: true, byteOrderMark: true) }) {
        var text = "Hello wörld\r\nsecond line \U0001F600 end\r\n";
        var contents = ReadContents(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());

        Assert.IsInstanceOfType(contents, typeof(Utf8FileContents), encoding.EncodingName);
        Assert.AreEqual(Encoding.UTF8.GetByteCount(text), contents.ByteLength, encoding.EncodingName);
        // Positions are UTF-16 positions in the decoded text (what the editors display).
        var position = text.IndexOf("end", StringComparison.Ordinal);
        var extract = contents.GetFileExtracts(100, new[] { new FilePositionSpan { Position = position, Length = 3 } }).Single();
        Assert.AreEqual("second line \U0001F600 end", extract.Text.TrimEnd('\r', '\n'), encoding.EncodingName);
        Assert.AreEqual(1, extract.LineNumber, encoding.EncodingName);
      }
    }

    [TestMethod]
    public void BinaryFilesAreDetectedFromTheirFirstBytesWithoutReadingThem() {
      // A NUL byte in the first 8,000 bytes (git's rule).
      var bytes = new byte[1024 * 1024];
      bytes.AsSpan().Fill((byte)'a');
      bytes[100] = 0;
      var path = WriteTempFile(bytes);
      try {
        var allocated = false;
        var slice = new PortableFileSystem().ReadFileContents(new FullPath(path), 2, TextKindDetector.BinaryDetectionPrefixLength,
          (prefix, size) => {
            if (TextKindDetector.IsBinaryPrefix(prefix))
              return default;
            allocated = true;
            return new NativeMemorySlice(NativeHeap.Alloc(size), 0, size, isDedicated: true);
          },
          out _, out var fileLength);
        Assert.IsFalse(allocated, "No memory must be allocated for a binary file");
        Assert.IsTrue(slice.IsNull, "The contents of a binary file must not be read");
        Assert.AreEqual(bytes.Length, fileLength);

        var contents = new FileContentsFactory(new PortableFileSystem()).ReadFileContents(new FullPath(path));
        Assert.IsInstanceOfType(contents, typeof(BinaryFileContents));
        Assert.AreEqual(bytes.Length, ((BinaryFileContents)contents).BinaryFileSize);
      }
      finally {
        File.Delete(path);
      }
    }

    [TestMethod]
    public void NulBytesAfterTheDetectionPrefixDontMakeFilesBinary() {
      var bytes = new byte[TextKindDetector.BinaryDetectionPrefixLength + 1000];
      bytes.AsSpan().Fill((byte)'a');
      bytes[TextKindDetector.BinaryDetectionPrefixLength] = 0;
      var contents = ReadContents(bytes);
      Assert.IsInstanceOfType(contents, typeof(Utf8FileContents));
      Assert.AreEqual(bytes.Length, contents.ByteLength);
    }

    [TestMethod]
    public void TextWithControlCharactersAndInvalidUtf8IsText() {
      // ANSI escape sequences, form feed, Latin-1 bytes: no NUL byte, so text (as git and ripgrep).
      var bytes = Encoding.ASCII.GetBytes("\u001b[31mred\u001b[0m\f\b\v").Concat(new byte[] { 0xE9, 0xE8, (byte)'\n' }).ToArray();
      var contents = ReadContents(bytes);
      Assert.IsInstanceOfType(contents, typeof(Utf8FileContents));
      Assert.AreEqual(bytes.Length, contents.ByteLength);
    }

    [TestMethod]
    public void SmallTextFilesShareAnArena() {
      var arenas = new ArenaAllocator();
      var first = ReadContents(Encoding.ASCII.GetBytes("first file"), arenas);
      var second = ReadContents(Encoding.ASCII.GetBytes("second file!"), arenas);

      Assert.IsInstanceOfType(first, typeof(Utf8FileContents));
      Assert.IsInstanceOfType(second, typeof(Utf8FileContents));
      // One arena, with both files and their 2 trailing NUL bytes (8 byte aligned slots).
      Assert.AreEqual(1, arenas.GetArenas().Count);
      Assert.AreEqual(16 + 16, arenas.GetArenas()[0].UsedBytes);
      Assert.AreEqual("second file!", ExtractText(second));
      GC.KeepAlive(first);
    }

    [TestMethod]
    public void LargeTextFilesGetADedicatedBlock() {
      var arenas = new ArenaAllocator();
      var bytes = new byte[ArenaAllocator.DefaultLargeAllocationSize + 1];
      bytes.AsSpan().Fill((byte)'a');
      var contents = ReadContents(bytes, arenas);

      Assert.IsInstanceOfType(contents, typeof(Utf8FileContents));
      Assert.AreEqual(bytes.Length, contents.ByteLength);
      Assert.AreEqual(0, arenas.GetArenas().Count, "Large files must not be allocated in an arena");
    }

    [TestMethod]
    public void BinaryAndUtf16FilesLeaveNoDeadBytesInArenas() {
      var arenas = new ArenaAllocator();
      var binary = new byte[1000];
      binary[10] = 0;
      Assert.IsInstanceOfType(ReadContents(binary, arenas), typeof(BinaryFileContents));
      Assert.AreEqual(0, arenas.GetArenas().Count, "Binary files must not be allocated in an arena");

      // UTF-16 files are read in a temporary block: only their UTF-8 conversion is in the arena.
      var encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: true);
      var contents = ReadContents(encoding.GetPreamble().Concat(encoding.GetBytes("utf-16 text")).ToArray(), arenas);
      Assert.IsInstanceOfType(contents, typeof(Utf8FileContents));
      Assert.AreEqual(1, arenas.GetArenas().Count);
      Assert.AreEqual(16, arenas.GetArenas()[0].UsedBytes);
      Assert.AreEqual("utf-16 text", ExtractText(contents));
    }

    private static string ExtractText(FileContents contents) {
      return contents.GetFileExtracts(100, new[] { new FilePositionSpan { Position = 0, Length = 1 } }).Single().Text;
    }

    private static FileContents ReadContents(byte[] bytes, ArenaAllocator arenas) {
      var path = WriteTempFile(bytes);
      try {
        return new FileContentsFactory(new PortableFileSystem(), arenas).ReadFileContents(new FullPath(path));
      }
      finally {
        File.Delete(path);
      }
    }

    private static FileContents ReadContents(byte[] bytes) {
      var path = WriteTempFile(bytes);
      try {
        return new FileContentsFactory(new PortableFileSystem()).ReadFileContents(new FullPath(path));
      }
      finally {
        File.Delete(path);
      }
    }

    private static string WriteTempFile(byte[] bytes) {
      var path = Path.Combine(Path.GetTempPath(), "VsChromiumTestFileContents-" + Guid.NewGuid().ToString("N") + ".txt");
      File.WriteAllBytes(path, bytes);
      return path;
    }

    /// <summary>
    /// Returns a block smaller than the requested trailing bytes, so that processing the contents
    /// fails.
    /// </summary>
    private class InvalidBlockFileSystem : PortableFileSystem, IFileContentsReader {
      public SafeNativeMemoryHandle LastBlock { get; private set; }

      public new NativeMemorySlice ReadFileContents(FullPath path, int trailingByteCount, int prefixLength,
        FileContentsAllocator allocate, out DateTime lastWriteTimeUtc, out long fileLength) {
        fileLength = 0;
        lastWriteTimeUtc = DateTime.UtcNow;
        LastBlock = NativeHeap.Alloc(Math.Max(0, trailingByteCount - 1));
        return new NativeMemorySlice(LastBlock, 0, LastBlock.ByteLength, isDedicated: true);
      }
    }
  }
}
