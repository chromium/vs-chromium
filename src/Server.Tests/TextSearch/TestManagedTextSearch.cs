// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.Core.Utility;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.TextSearch;

namespace VsChromium.Tests.TextSearch {
  [TestClass]
  public class TestManagedTextSearch {
    [TestMethod]
    public void LiteralSearchWorks() {
      AssertMatches("This is a piece of text", "piece", Options.MatchCase, (10, 5));
      AssertMatches("This is a piece of text", "Piece", Options.MatchCase);
      AssertMatches("This is a piece of text", "Piece", Options.None, (10, 5));
      AssertMatches("foo foo foo", "foo", Options.MatchCase, (0, 3), (4, 3), (8, 3));
    }

    [TestMethod]
    public void LiteralSearchMatchesDontOverlap() {
      AssertMatches("aaaaa", "aa", Options.MatchCase, (0, 2), (2, 2));
      AssertMatches("AaAaA", "aa", Options.None, (0, 2), (2, 2));
    }

    [TestMethod]
    public void LiteralSearchAtBoundariesWorks() {
      AssertMatches("ab", "ab", Options.MatchCase, (0, 2));
      AssertMatches("ab", "abc", Options.MatchCase);
      AssertMatches("xxab", "AB", Options.None, (2, 2));
      AssertMatches("ABxx", "ab", Options.None, (0, 2));
      AssertMatches("", "ab", Options.None);
    }

    [TestMethod]
    public void LiteralSearchIgnoreCaseWithNonLettersWorks() {
      AssertMatches("a::b A::B", "::", Options.None, (1, 2), (6, 2));
      AssertMatches("std::Unique_Ptr<int> std::unique_ptr", "STD::UNIQUE_PTR", Options.None, (0, 15), (21, 15));
      AssertMatches("x_q_y X_Q_Y", "_q_", Options.None, (1, 3), (7, 3));
    }

    [TestMethod]
    public void WholeWordSearchWorks() {
      AssertMatches("foo foobar barfoo foo_ (foo)", "foo", Options.MatchCase | Options.MatchWholeWord, (0, 3), (24, 3));
      AssertMatches("FOO foobar", "foo", Options.MatchWholeWord, (0, 3));
      // Non ASCII letters are word characters (UTF-16 text only).
      AssertUtf16Matches("éfoo foo", "foo", Options.MatchCase | Options.MatchWholeWord, (5, 3));
    }

    [TestMethod]
    public void NonAsciiPatternNeverMatchesAsciiText() {
      AssertAsciiMatches("cafe caf", "café", Options.MatchCase);
      AssertAsciiMatches("cafe caf", "café", Options.None);
    }

    [TestMethod]
    public void Utf16SearchWorks() {
      AssertUtf16Matches("Un café, deux cafés", "café", Options.MatchCase, (3, 4), (14, 4));
      AssertUtf16Matches("Un CAFÉ, deux cafés", "café", Options.None, (3, 4), (14, 4));
      AssertUtf16Matches("Ελληνικά ελληνικά", "ΕΛΛΗΝΙΚΆ", Options.None, (0, 8), (9, 8));
    }

    [TestMethod]
    public void RegexSearchWorks() {
      AssertMatches("void Foo::Bar(int x)", @"void \w+::\w+\(", Options.MatchCase | Options.Regex, (0, 14));
      AssertMatches("kFooBar = 10; kBlah = 2;", @"k[A-Z]\w+ = \d+", Options.MatchCase | Options.Regex, (0, 12), (14, 9));
      AssertMatches("FOO foo", "foo", Options.Regex, (0, 3), (4, 3));
      AssertMatches("FOO foo", "foo", Options.Regex | Options.MatchCase, (4, 3));
    }

    [TestMethod]
    public void RegexSearchLineAnchorsWorks() {
      // "^" and "$" match at line boundaries (multi-line mode).
      AssertMatches("#include a\n#include b\n  #include c", "^#include", Options.MatchCase | Options.Regex, (0, 8), (11, 8));
      AssertMatches("foo;\nbar;\nfoo", "foo$", Options.MatchCase | Options.Regex, (10, 3));
    }

    [TestMethod]
    public void RegexSearchDotDoesntMatchNewLine() {
      AssertMatches("ab\nab", "a.b", Options.MatchCase | Options.Regex);
      AssertMatches("ab\nab", "b.a", Options.MatchCase | Options.Regex);
    }

    [TestMethod]
    public void RegexEmptyMatchesHaveLengthOne() {
      AssertMatches("ab\ncd", "^", Options.MatchCase | Options.Regex, (0, 1), (3, 1));
    }

    [TestMethod]
    public void RegexWholeWordWorks() {
      AssertMatches("foo1 foo2 xfoo3", @"foo\d", Options.MatchCase | Options.Regex | Options.MatchWholeWord, (0, 4), (5, 4));
    }

    [TestMethod]
    public void RegexUtf16Works() {
      AssertUtf16Matches("Un café, deux cafés", "caf.s?", Options.MatchCase | Options.Regex, (3, 4), (14, 5));
    }

    [TestMethod]
    public void InvalidRegexThrowsRecoverableError() {
      Assert.ThrowsExactly<RecoverableErrorException>(() => CreateSearches("foo(", Options.Regex));
      Assert.ThrowsExactly<RecoverableErrorException>(() => CreateSearches("[a-", Options.Regex));
      // Constructs not supported by the non-backtracking engine (same as RE2).
      Assert.ThrowsExactly<RecoverableErrorException>(() => CreateSearches(@"(a)\1", Options.Regex));
      Assert.ThrowsExactly<RecoverableErrorException>(() => CreateSearches("foo(?=bar)", Options.Regex));
    }

    [TestMethod]
    public void FragmentPositionIsAddedToMatches() {
      var text = "0123456789foo456789foo";
      var bytes = Encoding.ASCII.GetBytes(text);
      CreateSearches("foo", Options.MatchCase, out var ascii, out var utf16);
      var expected = new[] { new TextRange(19, 3) };
      unsafe {
        fixed (byte* p = bytes) {
          var fragment = new TextFragment(new IntPtr(p), 12, text.Length - 12, 1);
          CollectionAssert.AreEqual(expected, ascii.FindAll(fragment, x => x, OperationProgressTracker.None).ToArray());
        }
        fixed (char* p = text) {
          var fragment = new TextFragment(new IntPtr(p), 12, text.Length - 12, 2);
          CollectionAssert.AreEqual(expected, utf16.FindAll(fragment, x => x, OperationProgressTracker.None).ToArray());
        }
      }
    }

    [TestMethod]
    public void FindFirstWorks() {
      CreateSearches("foo", Options.MatchCase, out var ascii, out _);
      var bytes = Encoding.ASCII.GetBytes("bar foo foo");
      unsafe {
        fixed (byte* p = bytes) {
          var fragment = new TextFragment(new IntPtr(p), 0, bytes.Length, 1);
          Assert.AreEqual(new TextRange(4, 3), ascii.FindFirst(fragment, OperationProgressTracker.None));
        }
      }
    }

    [TestMethod]
    public void PostProcessCanSkipMatches() {
      CreateSearches("foo", Options.MatchCase, out var ascii, out _);
      var bytes = Encoding.ASCII.GetBytes("foo foo foo");
      unsafe {
        fixed (byte* p = bytes) {
          var fragment = new TextFragment(new IntPtr(p), 0, bytes.Length, 1);
          var result = ascii.FindAll(fragment, x => x.Position == 4 ? (TextRange?)null : x, OperationProgressTracker.None);
          CollectionAssert.AreEqual(new[] { new TextRange(0, 3), new TextRange(8, 3) }, result.ToArray());
        }
      }
    }

    [TestMethod]
    public void LongPatternsWork() {
      var pattern = new string('x', 100) + "y";
      var text = new string('x', 1000) + "y" + new string('x', 1000);
      AssertMatches(text, pattern, Options.MatchCase, (900, 101));
      AssertMatches(text, pattern.ToUpperInvariant(), Options.None, (900, 101));
    }

    [Flags]
    public enum Options {
      None = 0,
      MatchCase = 1,
      MatchWholeWord = 2,
      Regex = 4,
    }

    public static void CreateSearches(string pattern, Options options) {
      CreateSearches(pattern, options, out var ascii, out var utf16);
      ascii.Dispose();
      utf16.Dispose();
    }

    public static void CreateSearches(string pattern, Options options, out ICompiledTextSearch ascii, out ICompiledTextSearch utf16) {
      TextSearchFactory.CreateSearches(
        pattern,
        options.HasFlag(Options.MatchCase),
        options.HasFlag(Options.MatchWholeWord),
        options.HasFlag(Options.Regex),
        out ascii,
        out utf16);
    }

    /// <summary>
    /// Checks the results for both ASCII and UTF-16 text.
    /// </summary>
    private static void AssertMatches(string text, string pattern, Options options, params (int, int)[] expected) {
      AssertAsciiMatches(text, pattern, options, expected);
      AssertUtf16Matches(text, pattern, options, expected);
    }

    private static void AssertAsciiMatches(string text, string pattern, Options options, params (int, int)[] expected) {
      var actual = SearchAscii(Encoding.ASCII.GetBytes(text), pattern, options);
      CollectionAssert.AreEqual(ToRanges(expected), actual, string.Format("ASCII search of \"{0}\" in \"{1}\"", pattern, text));
    }

    private static void AssertUtf16Matches(string text, string pattern, Options options, params (int, int)[] expected) {
      var actual = SearchUtf16(text, pattern, options);
      CollectionAssert.AreEqual(ToRanges(expected), actual, string.Format("UTF-16 search of \"{0}\" in \"{1}\"", pattern, text));
    }

    private static TextRange[] ToRanges((int, int)[] ranges) {
      return ranges.Select(x => new TextRange(x.Item1, x.Item2)).ToArray();
    }

    public static unsafe TextRange[] SearchAscii(byte[] text, string pattern, Options options) {
      CreateSearches(pattern, options, out var ascii, out var utf16);
      using (ascii)
      using (utf16) {
        fixed (byte* p = text) {
          var fragment = new TextFragment(new IntPtr(p), 0, text.Length, 1);
          return ascii.FindAll(fragment, x => x, OperationProgressTracker.None).ToArray();
        }
      }
    }

    public static unsafe TextRange[] SearchUtf16(string text, string pattern, Options options) {
      CreateSearches(pattern, options, out var ascii, out var utf16);
      using (ascii)
      using (utf16) {
        fixed (char* p = text) {
          var fragment = new TextFragment(new IntPtr(p), 0, text.Length, 2);
          return utf16.FindAll(fragment, x => x, OperationProgressTracker.None).ToArray();
        }
      }
    }
  }
}
