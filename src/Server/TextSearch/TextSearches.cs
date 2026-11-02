// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using VsChromium.Core.Utility;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Search in a text fragment containing characters of type <typeparamref
  /// name="TChar"/> (byte for ASCII text, char for UTF-16 text).
  /// </summary>
  public sealed class SpanTextSearch<TChar> : ManagedTextSearch where TChar : unmanaged {
    private readonly ITextMatcher<TChar> _matcher;

    public SpanTextSearch(ITextMatcher<TChar> matcher, bool matchWholeWord) : base(matchWholeWord) {
      _matcher = matcher;
    }

    protected override List<TextRange> Find(TextFragment textFragment, Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker, int maxResultSize) {
      var text = GetSpan<TChar>(textFragment);
      return FindMatches(text, textFragment.Position, _matcher, postProcess, progressTracker, maxResultSize);
    }
  }

  /// <summary>
  /// Regular expression search in ASCII text: .NET regular expressions only
  /// support UTF-16 text, so the ASCII text is widened into a temporary
  /// buffer first. Text fragments are small (see the "chunk size" of the file
  /// database), so this is relatively cheap.
  /// </summary>
  public sealed class AsciiRegexTextSearch : ManagedTextSearch {
    private readonly RegexMatcher _matcher;
    private readonly ITextMatcher<byte> _requiredLiteralMatcher;

    /// <param name="matcher">The regular expression.</param>
    /// <param name="requiredLiteralMatcher">Optional: search of a literal
    /// that all matches of the regular expression contain, used to skip text
    /// fragments without matches quickly (without converting them to
    /// UTF-16).</param>
    /// <param name="matchWholeWord"></param>
    public AsciiRegexTextSearch(RegexMatcher matcher, ITextMatcher<byte> requiredLiteralMatcher, bool matchWholeWord) : base(matchWholeWord) {
      _matcher = matcher;
      _requiredLiteralMatcher = requiredLiteralMatcher;
    }

    protected override List<TextRange> Find(TextFragment textFragment, Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker, int maxResultSize) {
      var bytes = GetSpan<byte>(textFragment);
      if (_requiredLiteralMatcher != null && !_requiredLiteralMatcher.FindNext(bytes, 0, out _, out _))
        return null;

      var buffer = ArrayPool<char>.Shared.Rent(Math.Max(bytes.Length, 1));
      try {
        // Latin1 is a 1 to 1 mapping of bytes to chars (we only have ASCII
        // bytes here anyways), so that character positions are preserved.
        var count = Encoding.Latin1.GetChars(bytes, buffer);
        var text = new ReadOnlySpan<char>(buffer, 0, count);
        return FindMatches(text, textFragment.Position, _matcher, postProcess, progressTracker, maxResultSize);
      }
      finally {
        ArrayPool<char>.Shared.Return(buffer);
      }
    }
  }

  /// <summary>
  /// Creates the search algorithms for ASCII and UTF-16 text.
  /// </summary>
  public static class TextSearchFactory {
    /// <summary>
    /// Create the search algorithms for <paramref name="pattern"/> for ASCII
    /// and UTF-16 text. Throws <see cref="VsChromium.Core.Ipc.RecoverableErrorException"/>
    /// if the pattern is not a valid regular expression.
    /// </summary>
    public static void CreateSearches(string pattern, bool matchCase, bool matchWholeWord, bool useRegex,
      out ICompiledTextSearch asciiSearch, out ICompiledTextSearch utf16Search) {
      if (useRegex) {
        var matcher = new RegexMatcher(RegexMatcher.CreateRegex(pattern, matchCase));
        asciiSearch = new AsciiRegexTextSearch(matcher, CreateRequiredLiteralMatcher(pattern, matchCase), matchWholeWord);
        utf16Search = new SpanTextSearch<char>(matcher, matchWholeWord);
        return;
      }

      asciiSearch = new SpanTextSearch<byte>(CreateAsciiLiteralMatcher(pattern, matchCase), matchWholeWord);
      utf16Search = new SpanTextSearch<char>(CreateUtf16LiteralMatcher(pattern, matchCase), matchWholeWord);
    }

    private static ITextMatcher<byte> CreateAsciiLiteralMatcher(string pattern, bool matchCase) {
      if (pattern.Length == 0 || !Ascii.IsValid(pattern)) {
        // A non ASCII pattern can't match ASCII text.
        return NoMatchMatcher<byte>.Instance;
      }
      var bytes = Encoding.ASCII.GetBytes(pattern);
      // Note: For case sensitive searches, Span.IndexOf is faster on the benchmarks.
      if (matchCase || !HasAsciiLetters(bytes))
        return new AsciiIndexOfMatcher(bytes);
      return new AsciiLiteralMatcher(bytes, matchCase: false);
    }

    private static ITextMatcher<byte> CreateRequiredLiteralMatcher(string pattern, bool matchCase) {
      var literal = RegexLiterals.GetRequiredLiteral(pattern);
      // Note: Non ASCII characters may match ASCII characters when ignoring
      // case (e.g. the Kelvin sign and "k"), so we only use ASCII literals.
      if (string.IsNullOrEmpty(literal) || !Ascii.IsValid(literal))
        return null;
      return CreateAsciiLiteralMatcher(literal, matchCase);
    }

    private static ITextMatcher<char> CreateUtf16LiteralMatcher(string pattern, bool matchCase) {
      if (pattern.Length == 0) {
        return NoMatchMatcher<char>.Instance;
      }
      return new Utf16LiteralMatcher(pattern, matchCase);
    }

    private static bool HasAsciiLetters(byte[] bytes) {
      foreach (var b in bytes) {
        if ((b >= 'a' && b <= 'z') || (b >= 'A' && b <= 'Z'))
          return true;
      }
      return false;
    }
  }
}
