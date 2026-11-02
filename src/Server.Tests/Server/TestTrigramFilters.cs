// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestTrigramFilters {
    private static readonly string[] Words = {
      "browser", "WebContents", "std::unique_ptr", "void", "return", "int", "const", "kMaxValue",
      "namespace", "#include", "<vector>", "TODO(", "class", "public:", "private:", "x", "y", "=", "+",
    };

    private static string RandomText(Random random, int length) {
      var sb = new StringBuilder();
      while (sb.Length < length) {
        sb.Append(Words[random.Next(Words.Length)]);
        sb.Append(random.Next(10) == 0 ? '\n' : ' ');
      }
      return sb.ToString(0, length);
    }

    [TestMethod]
    public void FiltersHaveNoFalseNegatives() {
      var random = new Random(42);
      for (var iteration = 0; iteration < 20; iteration++) {
        var text = RandomText(random, 5000);
        var ascii = Encoding.ASCII.GetBytes(text);
        var chunks = new[] { new TextRange(0, text.Length) };
        var asciiFilters = TrigramFilterSet.Build(ascii, chunks);
        var utf16Filters = TrigramFilterSet.Build(text.AsSpan(), chunks);
        for (var i = 0; i < 200; i++) {
          var start = random.Next(text.Length - 20);
          var substring = text.Substring(start, 3 + random.Next(15));
          foreach (var matchCase in new[] { true, false }) {
            // Case variations of a substring are found by case-insensitive searches
            var query = TrigramFilters.CreateQuery(new[] { matchCase ? substring : substring.ToUpperInvariant() }, matchCase);
            Assert.IsTrue(asciiFilters.MayContain(0, query), substring);
            if (matchCase) {
              Assert.IsTrue(utf16Filters.MayContain(0, query), substring);
            }
          }
        }
      }
    }

    [TestMethod]
    public void FiltersRejectMostAbsentTexts() {
      var random = new Random(7);
      var text = RandomText(random, 20000);
      var filters = TrigramFilterSet.Build(Encoding.ASCII.GetBytes(text), new[] { new TextRange(0, text.Length) });
      var rejected = 0;
      const int count = 1000;
      for (var i = 0; i < count; i++) {
        var absent = "zq" + i.ToString("D4") + "jx";
        Assert.IsFalse(text.Contains(absent));
        if (!filters.MayContain(0, TrigramFilters.CreateQuery(new[] { absent }, true)))
          rejected++;
      }
      Assert.IsTrue(rejected > count * 95 / 100, $"Only {rejected} of {count} absent texts rejected");
    }

    [TestMethod]
    public void EachChunkHasItsOwnFilter() {
      var text = "first chunk with alpha\nsecond chunk with beta\n";
      var chunks = new[] { new TextRange(0, 23), new TextRange(23, text.Length - 23) };
      var filters = TrigramFilterSet.Build(Encoding.ASCII.GetBytes(text), chunks);
      var alpha = TrigramFilters.CreateQuery(new[] { "alpha" }, true);
      var beta = TrigramFilters.CreateQuery(new[] { "beta" }, true);
      Assert.IsTrue(filters.MayContain(0, alpha));
      Assert.IsTrue(filters.MayContain(1, beta));
      Assert.IsFalse(filters.MayContain(0, beta));
      Assert.IsFalse(filters.MayContain(1, alpha));
    }

    [TestMethod]
    public void ShortTextsHaveNoQuery() {
      Assert.IsNull(TrigramFilters.CreateQuery(new[] { "ab" }, true));
      Assert.IsNull(TrigramFilters.CreateQuery(new string[] { null }, true));
      Assert.IsNotNull(TrigramFilters.CreateQuery(new[] { "ab", "cde" }, true));
    }
  }
}
