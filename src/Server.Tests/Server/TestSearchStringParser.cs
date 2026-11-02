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
      var result = parser.Parse("foo", SearchStringParserOptions.SupportsAsterisk);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("foo", result.LongestEntry.Text);
      Assert.AreEqual(0, result.LongestEntry.Index);
      Assert.AreEqual(0, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(0, result.EntriesAfterLongestEntry.Count);
    }

    [TestMethod]
    public void ParseNoEscapingWorks() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo*blah*bar", SearchStringParserOptions.NoSpecialCharacter);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("foo*blah*bar", result.LongestEntry.Text);
      Assert.AreEqual(0, result.LongestEntry.Index);
      Assert.AreEqual(0, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(0, result.EntriesAfterLongestEntry.Count);
    }

    [TestMethod]
    public void ParseMultipleAsterisksWorks() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo*blah*bar", SearchStringParserOptions.SupportsAsterisk);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("blah", result.LongestEntry.Text);
      Assert.AreEqual(1, result.LongestEntry.Index);
      Assert.AreEqual(1, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(1, result.EntriesAfterLongestEntry.Count);
    }

    [TestMethod]
    public void ParseMultipleAsterisksWorks2() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo*b**lah*bar", SearchStringParserOptions.SupportsAsteriskAndEscaping);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("b*lah", result.LongestEntry.Text);
      Assert.AreEqual(1, result.LongestEntry.Index);
      Assert.AreEqual(1, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(1, result.EntriesAfterLongestEntry.Count);
    }
    [TestMethod]
    public void ParseSimpleStringWorks2() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo*", SearchStringParserOptions.SupportsAsterisk);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("foo", result.LongestEntry.Text);
      Assert.AreEqual(0, result.LongestEntry.Index);
      Assert.AreEqual(0, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(0, result.EntriesAfterLongestEntry.Count);
    }
    [TestMethod]
    public void ParseInvalidStringWorks() {
      var parser = new SearchStringParser();
      var result = parser.Parse("*", SearchStringParserOptions.SupportsAsterisk);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("", result.LongestEntry.Text);
      Assert.AreEqual(0, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(0, result.EntriesAfterLongestEntry.Count);
    }
    [TestMethod]
    public void ParseInvalidStringWorks2() {
      var parser = new SearchStringParser();
      var result = parser.Parse("foo**bar2", SearchStringParserOptions.SupportsAsterisk);
      Assert.IsNotNull(result);
      Assert.IsNotNull(result.LongestEntry);
      Assert.AreEqual("bar2", result.LongestEntry.Text);
      Assert.AreEqual(1, result.LongestEntry.Index);
      Assert.AreEqual(1, result.EntriesBeforeLongestEntry.Count);
      Assert.AreEqual(0, result.EntriesAfterLongestEntry.Count);
    }
  }
}
