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
          int offset;
          int length;
          NativeMethods.Ascii_GetLineExtentFromPosition(
            mem.Ptr,
            mem.Size - 1,
            i,
            100,
            out offset,
            out length);
          Assert.AreEqual(0, offset);
          Assert.AreEqual(14, length);
        }
      }
    }

    [TestMethod]
    public void GetLineExtentForMultilineWorks() {
      using (var mem = CreateAsciiMemory("this is a test\nand another line\nline3")) {
        for (var i = 0; i <= 14; i++) {
          int offset;
          int length;
          NativeMethods.Ascii_GetLineExtentFromPosition(
            mem.Ptr,
            mem.Size - 1,
            i,
            100,
            out offset,
            out length);
          Assert.AreEqual(0, offset);
          Assert.AreEqual(15, length);
        }
        for (var i = 15; i <= 31; i++) {
          int offset;
          int length;
          NativeMethods.Ascii_GetLineExtentFromPosition(
            mem.Ptr,
            mem.Size - 1,
            i,
            100,
            out offset,
            out length);
          Assert.AreEqual(15, offset);
          Assert.AreEqual(17, length);
        }
        for (var i = 32; i <= 36; i++) {
          int offset;
          int length;
          NativeMethods.Ascii_GetLineExtentFromPosition(
            mem.Ptr,
            mem.Size - 1,
            i,
            100,
            out offset,
            out length);
          Assert.AreEqual(32, offset);
          Assert.AreEqual(5, length);
        }
      }
    }

    [TestMethod]
    public void GetLineExtentForEmptyWorks() {
      using (var mem = CreateAsciiMemory("\n\n\n")) {
        for (var i = 0; i <= 2; i++) {
          int offset;
          int length;
          NativeMethods.Ascii_GetLineExtentFromPosition(
            mem.Ptr,
            mem.Size - 1,
            i,
            100,
            out offset,
            out length);
          Assert.AreEqual(i, offset);
          Assert.AreEqual(1, length);
        }
      }
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
