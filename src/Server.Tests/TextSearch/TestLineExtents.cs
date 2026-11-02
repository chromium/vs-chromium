// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.TextSearch {
  [TestClass]
  public class TestLineExtents {
    [TestMethod]
    public void GetLineExtentForShortLineWorks() {
      const string text = "this is a test";
      for (var i = 0; i < text.Length; i++) {
        AssertExtent(text, i, 100, 0, 14);
      }
    }

    [TestMethod]
    public void GetLineExtentForMultilineWorks() {
      const string text = "this is a test\nand another line\nline3";
      for (var i = 0; i <= 14; i++) {
        AssertExtent(text, i, 100, 0, 15);
      }
      for (var i = 15; i <= 31; i++) {
        AssertExtent(text, i, 100, 15, 17);
      }
      for (var i = 32; i <= 36; i++) {
        AssertExtent(text, i, 100, 32, 5);
      }
    }

    [TestMethod]
    public void GetLineExtentForEmptyWorks() {
      const string text = "\n\n\n";
      for (var i = 0; i <= 2; i++) {
        AssertExtent(text, i, 100, i, 1);
      }
    }

    [TestMethod]
    public void GetLineExtentWithMaxLengthWorks() {
      const string text = "this is a test\nand another line\nline3";
      AssertExtent(text, 0, 3, 0, 3);
      AssertExtent(text, 1, 3, 0, 4);
      AssertExtent(text, 5, 3, 2, 6);
      AssertExtent(text, 13, 3, 10, 5);
      AssertExtent(text, 14, 3, 11, 4);
    }

    [TestMethod]
    public void GetLineExtentAtEndOfTextWorks() {
      AssertExtent("abc\ndef", 7, 100, 4, 3);
      AssertExtent("abc\n", 4, 100, 4, 0);
      AssertExtent("", 0, 100, 0, 0);
    }

    private static void AssertExtent(string text, int position, int maxLength, int expectedOffset, int expectedLength) {
      var ascii = LineExtents.GetLineExtentFromPosition<byte>(Encoding.ASCII.GetBytes(text), position, maxLength, (byte)'\n');
      Assert.AreEqual(expectedOffset, ascii.Position);
      Assert.AreEqual(expectedLength, ascii.Length);

      var utf16 = LineExtents.GetLineExtentFromPosition<char>(text.AsSpan(), position, maxLength, '\n');
      Assert.AreEqual(expectedOffset, utf16.Position);
      Assert.AreEqual(expectedLength, utf16.Length);
    }
  }
}
