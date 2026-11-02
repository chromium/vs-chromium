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
  /// name="TChar"/> (bytes for UTF-8 text, chars for UTF-16 text).
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
  /// Search in UTF-8 text (with non ASCII characters) with a UTF-16 matcher (regular expressions,
  /// case insensitive search of non ASCII text): the text fragment is decoded into a temporary
  /// UTF-16 buffer (same decoding as <see cref="Encoding.UTF8"/>: invalid bytes are replaced with
  /// U+FFFD), and the UTF-16 positions of the matches are converted back to UTF-8 (byte)
  /// positions.
  /// </summary>
  public sealed class Utf8TranscodingTextSearch : ManagedTextSearch {
    private readonly ITextMatcher<char> _matcher;
    private readonly ITextMatcher<byte> _requiredBytesMatcher;

    /// <param name="matcher">The UTF-16 search.</param>
    /// <param name="requiredBytesMatcher">Optional: search of bytes that all matches contain,
    /// used to skip text fragments without matches quickly (without decoding them).</param>
    /// <param name="matchWholeWord"></param>
    public Utf8TranscodingTextSearch(ITextMatcher<char> matcher, ITextMatcher<byte> requiredBytesMatcher, bool matchWholeWord) : base(matchWholeWord) {
      _matcher = matcher;
      _requiredBytesMatcher = requiredBytesMatcher;
    }

    protected override List<TextRange> Find(TextFragment textFragment, Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker, int maxResultSize) {
      var bytes = GetSpan<byte>(textFragment);
      if (_requiredBytesMatcher != null && !_requiredBytesMatcher.FindNext(bytes, 0, out _, out _))
        return null;

      var buffer = ArrayPool<char>.Shared.Rent(Math.Max(Encoding.UTF8.GetMaxCharCount(bytes.Length), 1));
      try {
        var count = Encoding.UTF8.GetChars(bytes, buffer);
        var text = new ReadOnlySpan<char>(buffer, 0, count);
        var mapper = new Utf16ToUtf8RangeMapper(bytes);
        return FindMatches(text, textFragment.Position, _matcher, ref mapper, postProcess, progressTracker, maxResultSize);
      }
      finally {
        ArrayPool<char>.Shared.Return(buffer);
      }
    }
  }

  /// <summary>
  /// Creates the search algorithms for ASCII, UTF-8 and UTF-16 text.
  /// </summary>
  public static class TextSearchFactory {
    /// <summary>
    /// Create the search algorithms for <paramref name="pattern"/> for ASCII text, UTF-8 text
    /// with non ASCII characters and UTF-16 text. Throws <see
    /// cref="VsChromium.Core.Ipc.RecoverableErrorException"/> if the pattern is not a valid
    /// regular expression.
    /// </summary>
    public static void CreateSearches(string pattern, bool matchCase, bool matchWholeWord, bool useRegex,
      out ICompiledTextSearch asciiSearch, out ICompiledTextSearch utf8Search, out ICompiledTextSearch utf16Search) {
      if (useRegex) {
        var matcher = new RegexMatcher(RegexMatcher.CreateRegex(pattern, matchCase));
        var requiredLiteral = RegexLiterals.GetRequiredLiteral(pattern);
        asciiSearch = new AsciiRegexTextSearch(matcher, CreateRequiredAsciiLiteralMatcher(requiredLiteral, matchCase), matchWholeWord);
        // Note: When ignoring case, the regular expression engine matches characters that are
        // not equal with ASCII case folding (e.g. the Kelvin sign and "k"), so the required
        // literal can't be used to skip UTF-8 text.
        var requiredBytes = matchCase ? CreateUtf8LiteralMatcher(requiredLiteral) : null;
        utf8Search = new Utf8TranscodingTextSearch(matcher, requiredBytes, matchWholeWord);
        utf16Search = new SpanTextSearch<char>(matcher, matchWholeWord);
        return;
      }

      asciiSearch = new SpanTextSearch<byte>(CreateAsciiLiteralMatcher(pattern, matchCase), matchWholeWord);
      utf8Search = CreateUtf8LiteralSearch(pattern, matchCase, matchWholeWord);
      utf16Search = new SpanTextSearch<char>(CreateUtf16LiteralMatcher(pattern, matchCase), matchWholeWord);
    }

    private static ICompiledTextSearch CreateUtf8LiteralSearch(string pattern, bool matchCase, bool matchWholeWord) {
      if (pattern.Length == 0)
        return new SpanTextSearch<byte>(NoMatchMatcher<byte>.Instance, matchWholeWord);

      if (Ascii.IsValid(pattern)) {
        // Note: Case insensitive search of an ASCII pattern uses ASCII case folding, same as in
        // ASCII text (see "Behavior changes" in PORTING_PRINCIPLES.md).
        return new SpanTextSearch<byte>(CreateAsciiLiteralMatcher(pattern, matchCase), matchWholeWord);
      }

      if (matchCase) {
        var bytesMatcher = CreateUtf8LiteralMatcher(pattern);
        if (bytesMatcher != null)
          return new SpanTextSearch<byte>(bytesMatcher, matchWholeWord);
      }

      // Case insensitive search of non ASCII text (Unicode simple case folding), or pattern
      // that can't be searched as bytes (e.g. U+FFFD, which matches invalid UTF-8 bytes).
      return new Utf8TranscodingTextSearch(CreateUtf16LiteralMatcher(pattern, matchCase), null, matchWholeWord);
    }

    private static ITextMatcher<byte> CreateAsciiLiteralMatcher(string pattern, bool matchCase) {
      if (pattern.Length == 0 || !Ascii.IsValid(pattern)) {
        // A non ASCII pattern can't match ASCII text.
        return NoMatchMatcher<byte>.Instance;
      }
      var bytes = Encoding.ASCII.GetBytes(pattern);
      // Note: For case sensitive searches, Span.IndexOf is faster on the benchmarks.
      if (matchCase || !HasAsciiLetters(bytes))
        return new BytesIndexOfMatcher(bytes);
      return new AsciiLiteralMatcher(bytes, matchCase: false);
    }

    private static ITextMatcher<byte> CreateRequiredAsciiLiteralMatcher(string literal, bool matchCase) {
      // Note: Non ASCII characters may match ASCII characters when ignoring
      // case (e.g. the Kelvin sign and "k"), so we only use ASCII literals.
      if (string.IsNullOrEmpty(literal) || !Ascii.IsValid(literal))
        return null;
      return CreateAsciiLiteralMatcher(literal, matchCase);
    }

    /// <summary>
    /// Case sensitive search of the UTF-8 bytes of <paramref name="literal"/>, or null if the
    /// literal is empty or if its matches in decoded text may not be matches of its bytes (U+FFFD
    /// or invalid UTF-16).
    /// </summary>
    private static ITextMatcher<byte> CreateUtf8LiteralMatcher(string literal) {
      var bytes = Utf8Literals.GetBytes(literal);
      if (bytes == null)
        return null;
      return new BytesIndexOfMatcher(bytes);
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

  public static class Utf8Literals {
    /// <summary>
    /// The UTF-8 bytes of <paramref name="literal"/>, or null if the literal is empty or if its
    /// occurrences in decoded UTF-8 text are not always occurrences of its bytes in the UTF-8
    /// text: U+FFFD (the replacement of invalid bytes) or invalid UTF-16 (lone surrogates).
    /// </summary>
    public static byte[] GetBytes(string literal) {
      if (string.IsNullOrEmpty(literal) || literal.Contains('\uFFFD'))
        return null;
      if (System.Text.Unicode.Utf8.FromUtf16(literal, new byte[Encoding.UTF8.GetMaxByteCount(literal.Length)],
            out _, out _, replaceInvalidSequences: false) != OperationStatus.Done)
        return null;
      return Encoding.UTF8.GetBytes(literal);
    }
  }
}
