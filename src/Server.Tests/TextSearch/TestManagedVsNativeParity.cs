// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Utility;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.TextSearch;
using Options = VsChromium.Tests.TextSearch.TestManagedTextSearch.Options;

namespace VsChromium.Tests.TextSearch {
  /// <summary>
  /// Parity tests of the managed search algorithms with the former native
  /// (C++) implementations, on random inputs. Temporary: to be deleted with
  /// VsChromium.Native.dll (porting milestone 4).
  /// </summary>
  [TestClass]
  public class TestManagedVsNativeParity {
    // A small alphabet, so that random patterns have many matches.
    private const string Alphabet = "abAB_1 :\n";

    [TestMethod]
    public void LiteralSearchMatchesNative() {
      var random = new Random(42);
      for (var i = 0; i < 2000; i++) {
        var text = RandomString(random, random.Next(0, 300));
        var pattern = RandomString(random, random.Next(2, 6));
        foreach (var options in new[] { Options.MatchCase, Options.None, Options.MatchCase | Options.MatchWholeWord, Options.MatchWholeWord }) {
          var bytes = Encoding.ASCII.GetBytes(text);
          var expected = SearchNative(NativeMethods.SearchAlgorithmKind.kBndm64, bytes, pattern, options);
          var actual = TestManagedTextSearch.SearchAscii(bytes, pattern, options);
          CollectionAssert.AreEqual(expected, actual, Describe(text, pattern, options));

          // Same results for UTF-16 text (ASCII characters only)
          var actualUtf16 = TestManagedTextSearch.SearchUtf16(text, pattern, options);
          CollectionAssert.AreEqual(expected, actualUtf16, Describe(text, pattern, options));
        }
      }
    }

    [TestMethod]
    public void LongLiteralSearchMatchesNative() {
      var random = new Random(43);
      for (var i = 0; i < 200; i++) {
        var text = RandomString(random, random.Next(0, 2000));
        var pattern = RandomString(random, random.Next(65, 80));
        // Make sure there is at least one match.
        var position = random.Next(0, text.Length);
        text = text.Substring(0, position) + pattern + text.Substring(position);
        foreach (var options in new[] { Options.MatchCase, Options.None }) {
          var bytes = Encoding.ASCII.GetBytes(text);
          var algo = (options & Options.MatchCase) != 0
            ? NativeMethods.SearchAlgorithmKind.kBoyerMoore
            : NativeMethods.SearchAlgorithmKind.kStrStr;
          var expected = SearchNative(algo, bytes, pattern, options);
          var actual = TestManagedTextSearch.SearchAscii(bytes, pattern, options);
          if ((options & Options.MatchCase) == 0) {
            // The native StrStr algorithm is case sensitive only.
            expected = TestManagedTextSearch.SearchUtf16(text, pattern, options);
          }
          Assert.IsTrue(expected.Length >= 1);
          CollectionAssert.AreEqual(expected, actual, Describe(text, pattern, options));
        }
      }
    }

    [TestMethod]
    public void RegexSearchMatchesNativeRe2() {
      // Patterns without line anchors ("^", "$") nor empty matches, as the
      // behavior intentionally changed for those.
      var patterns = new[] {
        "a+b", "[ab]+", @"\w+", @"a\d", "A.B", @"(ab|ba)_", @"\s+", "a{2,3}", @"[^a\n]+", @"\bab\b", "b[a-z]*A",
      };
      var random = new Random(44);
      for (var i = 0; i < 300; i++) {
        var text = RandomString(random, random.Next(0, 300));
        foreach (var pattern in patterns) {
          foreach (var options in new[] { Options.MatchCase | Options.Regex, Options.Regex, Options.Regex | Options.MatchWholeWord }) {
            var bytes = Encoding.ASCII.GetBytes(text);
            var expected = SearchNative(NativeMethods.SearchAlgorithmKind.kRe2, bytes, pattern, options);
            var actual = TestManagedTextSearch.SearchAscii(bytes, pattern, options);
            CollectionAssert.AreEqual(expected, actual, Describe(text, pattern, options));
          }
        }
      }
    }

    [TestMethod]
    public void TextKindMatchesNative() {
      var random = new Random(45);
      var alphabets = new[] {
        new byte[] { (byte)'a', (byte)' ', (byte)'\n' },
        new byte[] { (byte)'a', 0xC3, 0xA9, (byte)'\n' },
        new byte[] { (byte)'a', 0x00, 0x01 },
        new byte[] { (byte)'a', 0xE2, 0x82, 0xAC, 0xF0, 0x9F, 0x98, 0x80 },
        new byte[] { 0xEF, 0xBB, 0xBF, (byte)'a' },
      };
      for (var i = 0; i < 1000; i++) {
        var alphabet = alphabets[random.Next(alphabets.Length)];
        var bytes = new byte[random.Next(0, 500)];
        for (var j = 0; j < bytes.Length; j++) {
          bytes[j] = alphabet[random.Next(alphabet.Length)];
        }
        var expected = GetNativeKind(bytes);
        var actual = TextKindDetector.GetKind(bytes);
        // The managed version intentionally reports "UTF-8" instead of "ASCII"
        // for (mostly ASCII) text with invalid UTF-8 bytes.
        if (!Ascii.IsValid(TextKindDetector.HasUtf8Bom(bytes) ? bytes.AsSpan(3) : bytes)) {
          if (expected == TextKind.Ascii) expected = TextKind.Utf8;
          if (expected == TextKind.AsciiWithUtf8Bom) expected = TextKind.Utf8WithBom;
        }
        Assert.AreEqual(expected, actual, BitConverter.ToString(bytes));
      }
    }

    [TestMethod]
    public void LineExtentMatchesNative() {
      var random = new Random(46);
      for (var i = 0; i < 500; i++) {
        var text = RandomString(random, random.Next(0, 200));
        var bytes = Encoding.ASCII.GetBytes(text);
        for (var position = 0; position <= text.Length; position++) {
          var maxOffset = random.Next(1, 50);
          var expected = GetNativeLineExtent(bytes, position, maxOffset);
          var actual = LineExtents.GetLineExtentFromPosition<byte>(bytes, position, maxOffset, (byte)'\n');
          Assert.AreEqual(expected, actual, string.Format("Line extent at {0} (max offset {1}) in \"{2}\"", position, maxOffset, text));
          var actualUtf16 = LineExtents.GetLineExtentFromPosition<char>(text.AsSpan(), position, maxOffset, '\n');
          Assert.AreEqual(expected, actualUtf16);
        }
      }
    }

    private static string RandomString(Random random, int length) {
      var sb = new StringBuilder(length);
      for (var i = 0; i < length; i++) {
        sb.Append(Alphabet[random.Next(Alphabet.Length)]);
      }
      return sb.ToString();
    }

    private static string Describe(string text, string pattern, Options options) {
      return string.Format("Search of \"{0}\" ({1}) in \"{2}\"", pattern, options, text);
    }

    private static unsafe TextRange[] SearchNative(NativeMethods.SearchAlgorithmKind kind, byte[] text, string pattern, Options options) {
      var nativeOptions = NativeMethods.SearchOptions.kNone;
      if ((options & Options.MatchCase) != 0)
        nativeOptions |= NativeMethods.SearchOptions.kMatchCase;
      if ((options & Options.MatchWholeWord) != 0)
        nativeOptions |= NativeMethods.SearchOptions.kMatchWholeWord;
      using (var search = new AsciiCompiledTextSearchNative(kind, pattern, nativeOptions)) {
        fixed (byte* p = text) {
          var fragment = new TextFragment(new IntPtr(p), 0, text.Length, 1);
          return search.FindAll(fragment, x => x, OperationProgressTracker.None).ToArray();
        }
      }
    }

    private static unsafe TextKind GetNativeKind(byte[] bytes) {
      fixed (byte* p = bytes) {
        var kind = NativeMethods.Text_GetKind(new IntPtr(p), bytes.Length);
        switch (kind) {
          case NativeMethods.TextKind.TextKind_Ascii: return TextKind.Ascii;
          case NativeMethods.TextKind.TextKind_AsciiWithUtf8Bom: return TextKind.AsciiWithUtf8Bom;
          case NativeMethods.TextKind.TextKind_Utf8: return TextKind.Utf8;
          case NativeMethods.TextKind.TextKind_Utf8WithBom: return TextKind.Utf8WithBom;
          default: return TextKind.ProbablyBinary;
        }
      }
    }

    private static unsafe TextRange GetNativeLineExtent(byte[] bytes, int position, int maxOffset) {
      fixed (byte* p = bytes) {
        NativeMethods.Ascii_GetLineExtentFromPosition(new IntPtr(p), bytes.Length, position, maxOffset,
          out var start, out var length);
        return new TextRange(start, length);
      }
    }
  }
}
