// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.Search;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// Searches in UTF-8 file contents with non ASCII characters (and invalid UTF-8 bytes): the
  /// results (UTF-16 positions) must be the same as the same search in the UTF-16 decoding of the
  /// text (<see cref="Encoding.UTF8"/>), which is how such files were stored before.
  /// </summary>
  [TestClass]
  public class TestUtf8FileContents : ServerServicesTestBase {
    private ServerComponents _container;
    private ICompiledTextSearchDataFactory _factory;

    // Tokens of the random texts: ASCII, non ASCII (2, 3 and 4 bytes characters), characters
    // equal to ASCII letters when ignoring case with Unicode rules (Kelvin sign, long s, dotless
    // i), U+FFFD and invalid UTF-8 bytes.
    private static readonly byte[][] Tokens = new[] {
      "foo", "Foo", "FOO", "bar", " ", " ", " ", "\n", "\r\n", "_", "x", "(", "café", "CAFÉ", "naïve",
      "é", "日本語", "日本", "𝄞", "ſtr", "STR", "\u212Aelvin", "kelvin", "ın", "IN", "\uFFFD", "foo日", "𝄞foo",
    }.Select(x => Encoding.UTF8.GetBytes(x)).Concat(new[] {
      new byte[] { 0xE9 }, // Latin1 "é"
      new byte[] { 0xC3 }, // Truncated sequence
      new byte[] { 0x80 }, // Lone continuation byte
      new byte[] { 0xE6, 0x97 }, // Truncated 3 bytes sequence
      new byte[] { 0xF0, 0x9D, 0x84 }, // Truncated 4 bytes sequence
    }).ToArray();

    private static readonly string[] LiteralPatterns = {
      "foo", "FOO", "oo", "café", "CAFÉ", "日本", "本語", "𝄞", "𝄞foo", "str", "STR", "ſtr", "kelvin", "in", "ın",
      "\uFFFD\uFFFD", "é ", "o日", "foo bar", "\uFFFDf",
    };

    private static readonly string[] RegexPatterns = {
      "f[o]+", "caf.", "日.", @"\w+é", "kel", "KEL", "^foo", "bar$", "s?tr", "o.b", @"\bfoo\b", ".𝄞", "in",
    };

    [TestInitialize]
    public void Initialize() {
      _container = CreateServerServices();
      _factory = _container.CompiledTextSearchDataFactory;
    }

    [TestCleanup]
    public void Cleanup() {
      _container.Dispose();
    }

    [TestMethod]
    public void PositionsAreUtf16Positions() {
      var text = "é café\n日本語 café 𝄞 café";
      var contents = Utils.CreateUtf8FileContents(text);
      var result = Search(contents, "café", matchCase: true, wholeWord: false, regex: false, useFilters: false);
      CollectionAssert.AreEqual(new[] { (2, 4), (11, 4), (19, 4) }, result);
    }

    [TestMethod]
    public void InvalidBytesArePositionedAsReplacementCharacters() {
      // "\xE9ab\xE9 ab" (Latin1 "é"): each invalid byte is one U+FFFD character.
      var bytes = new byte[] { 0xE9, (byte)'a', (byte)'b', 0xE9, (byte)' ', (byte)'a', (byte)'b' };
      var contents = Utils.CreateUtf8FileContents(bytes);
      CollectionAssert.AreEqual(new[] { (1, 2), (5, 2) }, Search(contents, "ab", true, false, false, false));
      CollectionAssert.AreEqual(new[] { (0, 2) }, Search(contents, "\uFFFDa", true, false, false, false));
      CollectionAssert.AreEqual(new[] { (3, 2) }, Search(contents, "\uFFFD ", true, false, false, false));
    }

    [TestMethod]
    public void WholeWordUsesUnicodeLetters() {
      var contents = Utils.CreateUtf8FileContents("éfoo foo foo日 𝄞foo");
      // "foo" after "é" or before "日" is not a whole word; after "𝄞" (not in the BMP) it is.
      CollectionAssert.AreEqual(new[] { (5, 3), (16, 3) }, Search(contents, "foo", true, true, false, false));
    }

    [TestMethod]
    public void IgnoringCaseNeverMatchesNonAsciiWithAscii() {
      // Same as the UTF-16 search ("ordinal ignore case"): "str" doesn't match "ſtr" (long s), "in"
      // doesn't match "ın" (dotless i), "kelvin" doesn't match the Kelvin sign.
      var contents = Utils.CreateUtf8FileContents("ſtr STR ın IN \u212Aelvin");
      CollectionAssert.AreEqual(new[] { (4, 3) }, Search(contents, "str", false, false, false, false));
      CollectionAssert.AreEqual(new[] { (11, 2), (18, 2) }, Search(contents, "in", false, false, false, false));
      CollectionAssert.AreEqual(new (int, int)[0], Search(contents, "kelvin", false, false, false, false));
      // Regular expressions and non ASCII patterns use Unicode case folding.
      CollectionAssert.AreEqual(new[] { (14, 6) }, Search(contents, "kelvin", false, false, true, false));
      CollectionAssert.AreEqual(new[] { (0, 3) }, Search(contents, "ſTR", false, false, false, false));
    }

    [TestMethod]
    public void RandomTextsMatchUtf16Search() {
      var random = new Random(42);
      foreach (var length in new[] { 0, 10, 100, 1000, 10000 }) {
        for (var iteration = 0; iteration < 10; iteration++) {
          CheckRandomText(random, length, useFilters: iteration % 2 == 0);
        }
      }
    }

    [TestMethod]
    public void LargeRandomTextsMatchUtf16Search() {
      // Larger than the chunk size and the UTF-16 position checkpoints.
      var random = new Random(7);
      CheckRandomText(random, 350 * 1024, useFilters: false);
      CheckRandomText(random, 350 * 1024, useFilters: true);
    }

    private void CheckRandomText(Random random, int length, bool useFilters) {
      var bytes = RandomBytes(random, length);
      var text = Encoding.UTF8.GetString(bytes);
      var contents = Utils.CreateUtf8FileContents(bytes);
      if (useFilters)
        contents.BuildTrigramFilters();

      foreach (var regex in new[] { false, true }) {
        foreach (var pattern in regex ? RegexPatterns : LiteralPatterns) {
          foreach (var matchCase in new[] { true, false }) {
            foreach (var wholeWord in new[] { false, true }) {
              var expected = ExpectedMatches(text, pattern, matchCase, wholeWord, regex);
              var actual = Search(contents, pattern, matchCase, wholeWord, regex, useFilters);
              CollectionAssert.AreEqual(expected, actual,
                string.Format("Search of \"{0}\" (case: {1}, word: {2}, regex: {3}) in text of length {4}",
                  pattern, matchCase, wholeWord, regex, length));
            }
          }
        }
      }

      // Extracts of the matches contain the text of the matches.
      var matches = ExpectedMatches(text, "foo", false, false, false);
      var spans = matches.Take(50).Select(x => new FilePositionSpan { Position = x.Item1, Length = x.Item2 }).ToList();
      var extracts = contents.GetFileExtracts(100, spans).ToList();
      Assert.AreEqual(spans.Count, extracts.Count);
      for (var i = 0; i < spans.Count; i++) {
        Assert.AreEqual(text.Substring(extracts[i].Offset, extracts[i].Length), extracts[i].Text);
        Assert.IsTrue(extracts[i].Offset <= spans[i].Position);
        Assert.IsTrue(extracts[i].Offset + extracts[i].Length >= spans[i].Position + spans[i].Length);
      }
    }

    private static byte[] RandomBytes(Random random, int length) {
      var result = new List<byte>(length + 16);
      while (result.Count < length) {
        result.AddRange(Tokens[random.Next(Tokens.Length)]);
      }
      return result.ToArray();
    }

    /// <summary>
    /// The matches of the search in the UTF-16 text: the reference implementation.
    /// </summary>
    private static (int, int)[] ExpectedMatches(string text, string pattern, bool matchCase, bool wholeWord, bool regex) {
      var result = new List<(int, int)>();
      if (regex) {
        // Per line (chunks end with new lines), same as RegexMatcher otherwise.
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture | RegexOptions.Multiline;
        if (!matchCase)
          options |= RegexOptions.IgnoreCase;
        var re = new Regex(pattern, options);
        foreach (var match in re.EnumerateMatches(text)) {
          var matchLength = match.Length == 0 && match.Index < text.Length ? 1 : match.Length;
          if (!wholeWord || IsWholeWord(text, match.Index, matchLength)) {
            // Note: Matches can't start or end in the middle of a surrogate pair in UTF-8 text
            // (e.g. "." matches a single UTF-16 code unit): the whole character is included.
            var start = match.Index;
            var end = match.Index + matchLength;
            if (start > 0 && start < text.Length && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
              start--;
            if (end > 0 && end < text.Length && char.IsLowSurrogate(text[end]) && char.IsHighSurrogate(text[end - 1]))
              end++;
            result.Add((start, end - start));
          }
        }
        return result.ToArray();
      }

      var index = 0;
      while (index + pattern.Length <= text.Length) {
        var isMatch = string.Compare(text, index, pattern, 0, pattern.Length,
          matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) == 0;
        if (isMatch) {
          if (!wholeWord || IsWholeWord(text, index, pattern.Length))
            result.Add((index, pattern.Length));
          index += pattern.Length;
        } else {
          index++;
        }
      }
      return result.ToArray();
    }

    private static bool IsWholeWord(string text, int index, int length) {
      if (index > 0 && WordCharacters.IsWordCharacter(text[index - 1]))
        return false;
      if (index + length < text.Length && WordCharacters.IsWordCharacter(text[index + length]))
        return false;
      return true;
    }

    private (int, int)[] Search(FileContents contents, string pattern, bool matchCase, bool wholeWord, bool regex, bool useFilters) {
      var searchParams = new SearchParams {
        SearchString = pattern,
        MatchCase = matchCase,
        MatchWholeWord = wholeWord,
        MaxResults = int.MaxValue,
        Regex = regex,
      };
      using (var searchData = _factory.Create(searchParams, x => true)) {
        var result = new List<(int, int)>();
        var chunks = contents.GetChunks();
        for (var i = 0; i < chunks.Count; i++) {
          if (useFilters && searchData.TrigramQuery != null && !contents.MayContain(i, searchData.TrigramQuery))
            continue;
          result.AddRange(contents.FindAll(searchData, chunks[i], OperationProgressTracker.None)
            .Select(x => (x.Position, x.Length)));
        }
        return result.ToArray();
      }
    }
  }
}
