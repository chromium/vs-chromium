// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemContents;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileContentsGetFileExtracts {
    [TestMethod]
    public void GetFileExtractsForSingleLineWorks() {
      const string text = "This is a piece of text";
      var result = PerformGetFileExtracts(
        text,
        50,
        new FilePositionSpan {
          Position = 0,
          Length = 5
        });
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(0, result[0].LineNumber);
      Assert.AreEqual(0, result[0].ColumnNumber);
      Assert.AreEqual(0, result[0].Offset);
      Assert.AreEqual(text.Length, result[0].Length);
      Assert.AreEqual(text, result[0].Text);
    }

    [TestMethod]
    public void GetFileExtractsForSingleLineWorks2() {
      const string text = "This is a piece of text";
      var result = PerformGetFileExtracts(
        text,
        10,
        new FilePositionSpan {
          Position = 0,
          Length = 5
        });
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(0, result[0].LineNumber);
      Assert.AreEqual(0, result[0].ColumnNumber);
      Assert.AreEqual(0, result[0].Offset);
      Assert.AreEqual(10, result[0].Length);
      Assert.AreEqual("This is a ", result[0].Text);
    }

    [TestMethod]
    public void GetFileExtractsForSingleLineWorks3() {
      const string text = "This is a piece of text";
      var result = PerformGetFileExtracts(
        text,
        10,
        new FilePositionSpan {
          Position = 10,
          Length = 5
        });
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(0, result[0].LineNumber);
      Assert.AreEqual(10, result[0].ColumnNumber);
      Assert.AreEqual(5, result[0].Offset);
      Assert.AreEqual(10, result[0].Length);
      Assert.AreEqual("is a piece", result[0].Text);
    }

    [TestMethod]
    public void GetFileExtractsForSingleLineWorks4() {
      const string text = "This is a piece of text";
      var result = PerformGetFileExtracts(
        text,
        10,
        new FilePositionSpan {
          Position = 21,
          Length = 2
        });
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual(0, result[0].LineNumber);
      Assert.AreEqual(21, result[0].ColumnNumber);
      Assert.AreEqual(13, result[0].Offset);
      Assert.AreEqual(10, result[0].Length);
      Assert.AreEqual("ce of text", result[0].Text);
    }

    [TestMethod]
    public void GetFileExtractsForMultiLineWorks() {
      const string text = "This is\na piece\nof text";
      var result = PerformGetFileExtracts(
        text,
        10,
        new FilePositionSpan {
          Position = 11,
          Length = 3
        });
      Assert.AreEqual(1, result.Count);
      Assert.AreEqual("a piece\n", result[0].Text);
      Assert.AreEqual(1, result[0].LineNumber);
      Assert.AreEqual(3, result[0].ColumnNumber);
      Assert.AreEqual(8, result[0].Offset);
      Assert.AreEqual(8, result[0].Length);
    }

    private IList<FileExtract> PerformGetFileExtracts(string text, int maxLength, params FilePositionSpan[] spans) {
      var result1 = PerformGetFileExtracts(() => Utils.CreateAsciiFileContents(text), maxLength, spans);
      var result2 = PerformGetFileExtracts(() => Utils.CreateUtf16FileContents(text), maxLength, spans);
      Assert.IsTrue(result1.SequenceEqual(result2, new FileExtractComparer()));
      return result1;
    }

    private IList<FileExtract> PerformGetFileExtracts(
      Func<FileContents> fileContentsFactory,
      int maxLength,
      params FilePositionSpan[] spans) {
      var contents = fileContentsFactory();
      return contents.GetFileExtracts(maxLength, spans).ToList();
    }
  }

  internal class FileExtractComparer : IEqualityComparer<FileExtract> {
    public bool Equals(FileExtract x, FileExtract y) {
      if (object.ReferenceEquals(x, y))
        return true;
      if (x == null || y == null)
        return false;

      return x.Offset == y.Offset &&
             x.Length == y.Length &&
             x.LineNumber == y.LineNumber &&
             x.ColumnNumber == y.ColumnNumber &&
             x.Text == y.Text;
    }

    public int GetHashCode(FileExtract obj) {
      return HashCode.Combine(obj.Offset, obj.Length);
    }
  }
}
