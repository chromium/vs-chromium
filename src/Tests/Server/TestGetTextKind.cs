// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestGetTextKind {
    [TestMethod]
    public unsafe void GetTextKindForAsciiWorks() {
      var bytes = new byte[] { 0x54, 0x68, 0x01, 0x7f };
      fixed (byte* array = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(array), bytes.Length);
        Assert.AreEqual(NativeMethods.TextKind.Ascii, kind);
      }
    }

    [TestMethod]
    public unsafe void GetTextKindForAsciiWithUtf8BomWorks() {
      var bytes = new byte[] { 0xef, 0xbb, 0xbf, 0x54, 0x68 };
      fixed (byte* array = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(array), bytes.Length);
        Assert.AreEqual(NativeMethods.TextKind.AsciiWithUtf8Bom, kind);
      }
    }

    [TestMethod]
    public unsafe void GetTextKindForUtf8WithBomWorks() {
      var bytes = new byte[] { 0xef, 0xbb, 0xbf, 0xef, 0x68 };
      fixed (byte* array = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(array), bytes.Length);
        Assert.AreEqual(NativeMethods.TextKind.Utf8WithBom, kind);
      }
    }

    [TestMethod]
    public unsafe void GetTextKindForUnknownWorks() {
      var bytes = new byte[] { 0xbb, 0xbb, 0xbe, 0xbe };
      fixed (byte* array = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(array), bytes.Length);
        Assert.AreEqual(NativeMethods.TextKind.Unknown, kind);
      }
    }
  }
}
