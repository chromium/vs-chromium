// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.TextSearch {
  [TestClass]
  public class TestRegexLiterals {
    [TestMethod]
    [DataRow("foo", "foo")]
    [DataRow("VSCB_UNIQUE_MARKER_0[0-4]", "VSCB_UNIQUE_MARKER_0")]
    [DataRow(@"void \w+::\w+\(", "void ")]
    [DataRow(@"k[A-Z]\w+ = \d+", " = ")]
    [DataRow(@"foo\.bar", "foo.bar")]
    [DataRow("abc*", "ab")]
    [DataRow("abcd?ef", "abc")]
    [DataRow("ab{2,3}cdef", "cdef")]
    [DataRow("a+?bcd", "bcd")]
    [DataRow("(foo|bar)bazz", "bazz")]
    [DataRow("(a(b)c)d", "d")]
    [DataRow("^#include", "#include")]
    [DataRow("foo$", "foo")]
    [DataRow("[]a]bc", "bc")]
    [DataRow(@"[\]x]yz", "yz")]
    [DataRow("a.bc", "bc")]
    [DataRow(@"\bfoo\b", "foo")]
    public void RequiredLiteralIsFound(string pattern, string expected) {
      Assert.AreEqual(expected, RegexLiterals.GetRequiredLiteral(pattern));
    }

    [TestMethod]
    [DataRow("foo|bar")]
    [DataRow("(?i)foo")]
    [DataRow("a(?=b)")]
    [DataRow(@"\w+")]
    [DataRow("[a-z]+")]
    [DataRow("a*")]
    [DataRow("*a")]
    [DataRow("(abc")]
    [DataRow("")]
    public void NoRequiredLiteral(string pattern) {
      Assert.IsNull(RegexLiterals.GetRequiredLiteral(pattern));
    }
  }
}
