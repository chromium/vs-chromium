// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Tests.NativeInterop {
  [TestClass]
  public class TestGetLineExtent {
    [TestMethod]
    public void GetLineExtentForShortLineWorks() {
      using (var mem = CreateAsciiMemory("this is a test")) {
        for (var i = 0; i < mem.Size - 1; i++) {
          AssertExtent(mem, i, 100, 0, 14);
        }
      }
    }

    [TestMethod]
    public void GetLineExtentForMultilineWorks() {
      using (var mem = CreateAsciiMemory("this is a test\nand another line\nline3")) {
        for (var i = 0; i <= 14; i++) {
          AssertExtent(mem, i, 100, 0, 15);
        }
        for (var i = 15; i <= 31; i++) {
          AssertExtent(mem, i, 100, 15, 17);
        }
        for (var i = 32; i <= 36; i++) {
          AssertExtent(mem, i, 100, 32, 5);
        }
      }
    }

    [TestMethod]
    public void GetLineExtentForEmptyWorks() {
      using (var mem = CreateAsciiMemory("\n\n\n")) {
        for (var i = 0; i <= 2; i++) {
          AssertExtent(mem, i, 100, i, 1);
        }
      }
    }

    [TestMethod]
    public void GetLineExtentWithMaxLengthWorks() {
      using (var mem = CreateAsciiMemory("this is a test\nand another line\nline3")) {
        AssertExtent(mem, 0, 3, 0, 3);
        AssertExtent(mem, 1, 3, 0, 4);
        AssertExtent(mem, 5, 3, 2, 6);
        AssertExtent(mem, 13, 3, 10, 5);
        AssertExtent(mem, 14, 3, 11, 4);
      }
    }

    public void AssertExtent(MemoryBlock mem, int position, int maxLength, int expectedOffset, int expectedLength) {
      int offset;
      int length;
      NativeMethods.Ascii_GetLineExtentFromPosition(
        mem.Ptr,
        mem.Size - 1,
        position,
        maxLength,
        out offset,
        out length);
      Assert.AreEqual(expectedOffset, offset);
      Assert.AreEqual(expectedLength, length);
    }

    public static MemoryBlock CreateAsciiMemory(string text) {
      var size = text.Length + 1;
      var handle = new SafeHGlobalHandle(Marshal.StringToHGlobalAnsi(text));
      return new MemoryBlock(handle, size);
    }

    public struct MemoryBlock : IDisposable {
      private readonly SafeHandle _handle;
      private readonly int _size;

      public MemoryBlock(SafeHandle handle, int size) {
        _handle = handle;
        _size = size;
      }

      public IntPtr Ptr {
        get { return _handle.DangerousGetHandle(); }
      }

      public int Size {
        get { return _size; }
      }

      public void Dispose() {
        _handle.Dispose();
      }
    }
  }
}
