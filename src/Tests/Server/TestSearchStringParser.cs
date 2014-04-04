// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Search;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestSearchStringParser {
    [TestMethod]
    public void ParseSimpleStringWorks() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo");
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.MainEntry);
      Assert.AreEqual("foo", result.MainEntry.Text);
      Assert.AreEqual(0, result.MainEntry.Index);
    }
    [TestMethod]
    public void ParseSimpleStringWorks2() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo*");
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.MainEntry);
      Assert.AreEqual("foo", result.MainEntry.Text);
      Assert.AreEqual(0, result.MainEntry.Index);
    }
    [TestMethod]
    public void ParseInvalidStringWorks() {
      var parser = new SearchStringParser();
      var result = parser.Parse("*");
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.MainEntry);
      Assert.AreEqual("", result.MainEntry.Text);
    }
    [TestMethod]
    public void ParseInvalidStringWorks2() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo**bar2");
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.MainEntry);
      Assert.AreEqual("bar2", result.MainEntry.Text);
      Assert.AreEqual(1, result.MainEntry.Index);
    }
  }
}
