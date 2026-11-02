// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
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
    public void GetTextKindForBinaryWorks() {
      var bytes = new byte[] {
        0xbb, 0xbb, 0xbe, 0xbe // random binary values
      };
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFilesWorks() {
      CheckKind(ReadTestFile("files\\bear.ac3"), TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithSomeAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 60% ascii and 40% binary.
      var bytes = CreateArray(100, 0.60);
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForLargeBinaryFileWithSomeAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 60% ascii and 40% binary.
      var bytes = CreateArray(1 * 1024 * 1024 /*1 MB*/, 0.60);
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForMediumBinaryFileWithSomeAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 60% ascii and 40% binary.
      var bytes = CreateArray(180 * 1024 /* 180 KB */, 0.60);
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithMostlyAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 80% ascii and 20% binary.
      var bytes = CreateArray(100, 0.80);
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithAlmostOnlyAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 95% ascii and 5% binary.
      // Note: The native implementation reported "ASCII", which was incorrect,
      // as the text contains non ASCII bytes (see "Behavior changes" in
      // PORTING_PRINCIPLES.md).
      var bytes = CreateArray(100, 0.95);
      CheckKind(bytes, TextKind.Utf8);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithMostlyBinaryWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 5% ascii and 9% binary.
      var bytes = CreateArray(100, 0.05);
      CheckKind(bytes, TextKind.ProbablyBinary);
    }

    private static byte[] CreateArray(int size, double asciiPercentage) {
      var result = new byte[size];

      int sliceSize = Math.Min(size / 5, 100);
      int threshold = (int)Math.Round(asciiPercentage * sliceSize);
      for (var i = 0; i < size; i++) {
        result[i] = ((i % sliceSize) >= threshold) ? (byte)0xbb : (byte)0x20;
      }
      return result;
    }

    private static void CheckKind(byte[] bytes, TextKind expectedKind) {
      var kind = TextKindDetector.GetKind(bytes);
      Assert.AreEqual(expectedKind, kind);
    }

    private byte[] ReadTestFile(string name) {
      var dir = Utils.GetTestDataDirectory();
      var path = Path.Combine(dir.FullName, name);
      Assert.IsTrue(File.Exists(path));
      using (var stream = new FileStream(path, FileMode.Open)) {
        var memStream = new MemoryStream();
        var buffer = new byte[4096];
        while (true) {
          int count = stream.Read(buffer, 0, buffer.Length);
          if (count == 0)
            break;
          memStream.Write(buffer, 0, count);
        }
        return memStream.ToArray();
      }
    }
  }
}
