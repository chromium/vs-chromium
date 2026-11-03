// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.TextSearch;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.TextSearch {
  [TestClass]
  public class TestTextKindDetector {
    [TestMethod]
    public void GetTextKindForAsciiWorks() {
      var bytes = new byte[] {
        0x54, 0x68, 0x20, 0x60 // ascii
      };
      CheckKind(bytes, TextKind.Ascii);
    }

    [TestMethod]
    public void GetTextKindForEmptyFileWorks() {
      CheckKind(new byte[0], TextKind.Ascii);
    }

    [TestMethod]
    public void GetTextKindForAsciiWithUtf8BomWorks() {
      var bytes = new byte[] {
        0xef, 0xbb, 0xbf, // bom
        0x54, 0x68 // ascii
      };
      CheckKind(bytes, TextKind.AsciiWithUtf8Bom);
    }

    [TestMethod]
    public void GetTextKindForUtf8WithBomWorks() {
      var bytes = new byte[] {
        0xef, 0xbb, 0xbf, // bom
        0xcf, 0x95, // seq2
        0xe5, 0xaa, 0x95, // seq3
        0xf5, 0xaa, 0xaa, 0x95, // seq4
        0x20, // ascii
        0x21, // ascii
        0x22, // ascii
      };
      CheckKind(bytes, TextKind.Utf8WithBom);
    }

    [TestMethod]
    public void GetTextKindForUtf8Works() {
      var bytes = new byte[] {
        0xcf, 0x95, // seq2
        0xe5, 0xaa, 0x95, // seq3
        0xf5, 0xaa, 0xaa, 0x95, // seq4
        0x20, // ascii
        0x21, // ascii
        0x22, // ascii
      };
      CheckKind(bytes, TextKind.Utf8);
    }

    [TestMethod]
    public void GetTextKindForUtf16WithBomWorks() {
      CheckKind(new byte[] { 0xff, 0xfe }.Concat(Encoding.Unicode.GetBytes("text")).ToArray(), TextKind.Utf16LittleEndianWithBom);
      CheckKind(new byte[] { 0xfe, 0xff }.Concat(Encoding.BigEndianUnicode.GetBytes("text")).ToArray(), TextKind.Utf16BigEndianWithBom);
      // UTF-16 without BOM: NUL bytes, binary (as git).
      CheckKind(Encoding.Unicode.GetBytes("text"), TextKind.Binary);
      // UTF-32 little endian BOM (FF FE 00 00): not supported, binary.
      CheckKind(new byte[] { 0xff, 0xfe, 0, 0 }.Concat(Encoding.UTF32.GetBytes("text")).ToArray(), TextKind.Binary);
    }

    [TestMethod]
    public void NulByteInTheFirst8000BytesMeansBinary() {
      var bytes = new byte[100_000];
      bytes.AsSpan().Fill((byte)'a');
      bytes[TextKindDetector.BinaryDetectionPrefixLength - 1] = 0;
      CheckKind(bytes, TextKind.Binary);
      Assert.IsTrue(TextKindDetector.IsBinaryPrefix(bytes.AsSpan(0, TextKindDetector.BinaryDetectionPrefixLength)));
    }

    [TestMethod]
    public void NulByteAfterTheFirst8000BytesIsText() {
      var bytes = new byte[100_000];
      bytes.AsSpan().Fill((byte)'a');
      bytes[TextKindDetector.BinaryDetectionPrefixLength] = 0;
      CheckKind(bytes, TextKind.Ascii);
      Assert.IsFalse(TextKindDetector.IsBinaryPrefix(bytes.AsSpan(0, TextKindDetector.BinaryDetectionPrefixLength)));
    }

    [TestMethod]
    public void NulBytesInUtf8OrUtf16FilesWithBomAreText() {
      CheckKind(new byte[] { 0xef, 0xbb, 0xbf, (byte)'a', 0, (byte)'b' }, TextKind.AsciiWithUtf8Bom);
      Assert.IsFalse(TextKindDetector.IsBinaryPrefix(new byte[] { 0xff, 0xfe, (byte)'a', 0 }));
    }

    [TestMethod]
    public void NonTextBytesWithoutNulAreText() {
      // Control characters (ESC, form feed, backspace) and invalid UTF-8 (e.g. Latin-1): text, as
      // git and ripgrep (the former heuristic reported binary above 10% of such bytes).
      CheckKind(new byte[] { 0x1b, (byte)'[', (byte)'m', 0x0c, 0x08, 0xbb, 0xbb, 0xbe, 0xbe }, TextKind.Utf8);
    }

    [TestMethod]
    public void GetTextKindForBinaryFilesWorks() {
      CheckKind(ReadTestFile("files\\bear.ac3"), TextKind.Binary);
    }

    private static void CheckKind(byte[] bytes, TextKind expectedKind) {
      var kind = TextKindDetector.GetKind(bytes);
      Assert.AreEqual(expectedKind, kind);
      // The binary decision only depends on the prefix.
      Assert.AreEqual(expectedKind == TextKind.Binary,
        TextKindDetector.IsBinaryPrefix(bytes.AsSpan(0, Math.Min(bytes.Length, TextKindDetector.BinaryDetectionPrefixLength))));
    }

    private byte[] ReadTestFile(string name) {
      var dir = Utils.GetTestDataDirectory();
      var path = Path.Combine(dir.FullName, name);
      Assert.IsTrue(File.Exists(path));
      return File.ReadAllBytes(path);
    }
  }
}
