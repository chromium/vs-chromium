// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.NativeInterop;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.NativeInterop {
  [TestClass]
  public class TestGetTextKind {
    [TestMethod]
    public void GetTextKindForAsciiWorks() {
      var bytes = new byte[] {
        0x54, 0x68, 0x20, 0x60 // ascii
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_Ascii);
    }

    [TestMethod]
    public void GetTextKindForAsciiWithUtf8BomWorks() {
      var bytes = new byte[] {
        0xef, 0xbb, 0xbf, // bom
        0x54, 0x68 // ascii
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_AsciiWithUtf8Bom);
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
      CheckKind(bytes, NativeMethods.TextKind.TextKind_Utf8WithBom);
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
      CheckKind(bytes, NativeMethods.TextKind.TextKind_Utf8);
    }

    [TestMethod]
    public void GetTextKindForBinaryWorks() {
      var bytes = new byte[] {
        0xbb, 0xbb, 0xbe, 0xbe // random binary values
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFilesWorks() {
      CheckKind(ReadTestFile("files\\bear.ac3"), NativeMethods.TextKind.TextKind_ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithSomeAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 60% ascii and 40% binary.
      var bytes = new byte[] {
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbb, 0xbb, 0xbe, 0xbe,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbb, 0xbb, 0xbe, 0xbe,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbb, 0xbb, 0xbe, 0xbe,
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithMostlyAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 80% ascii and 20% binary.
      var bytes = new byte[] {
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbe, 0xbe,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbe, 0xbe,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbe, 0xbe,
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_ProbablyBinary);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithAlmostOnlyAsciiWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 95% ascii and 5% binary.
      var bytes = new byte[] {
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20,
        0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0x20, 0xbe, 0xbe,
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_Ascii);
    }

    [TestMethod]
    public void GetTextKindForBinaryFileWithMostlyBinaryWorks() {
      // Ensure minimum ratio of 90% is computed correctly. Create an binary sequence
      // of about 5% ascii and 9% binary.
      var bytes = new byte[] {
        0x20, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbe, 0xbe,
        0x20, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbe, 0xbe,
        0x20, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbb, 0xbe, 0xbe,
      };
      CheckKind(bytes, NativeMethods.TextKind.TextKind_ProbablyBinary);
    }


    private static unsafe void CheckKind(byte[] bytes, NativeMethods.TextKind expectedKind) {
      fixed (byte* array = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(array), bytes.Length);
        Assert.AreEqual(expectedKind, kind);
      }
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
