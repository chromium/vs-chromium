// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using VsChromium.Core.Ipc;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Case sensitive search of a literal in ASCII text.
  /// </summary>
  public sealed class AsciiLiteralMatcher : ITextMatcher<byte> {
    private readonly byte[] _pattern;

    public AsciiLiteralMatcher(byte[] pattern) {
      _pattern = pattern;
    }

    public bool FindNext(ReadOnlySpan<byte> text, int start, out int matchIndex, out int matchLength) {
      matchLength = _pattern.Length;
      var index = text.Slice(start).IndexOf(_pattern);
      matchIndex = start + index;
      return index >= 0;
    }
  }

  /// <summary>
  /// Case insensitive search of a literal in ASCII text: looks for the least
  /// frequent character of the pattern (as estimated for source code) with a
  /// vectorized search, then compares the whole pattern at each candidate
  /// position.
  /// </summary>
  public sealed class AsciiLiteralIgnoreCaseMatcher : ITextMatcher<byte> {
    // ASCII characters ordered by decreasing (approximate) frequency in source
    // code, letters in lower case. Characters not in this list are considered
    // the least frequent.
    private const string CharactersByFrequency =
      " etainorslcdu_hpm(f)gb.;,=v*/kwyx-\"0>1<{}:[]2&qz'3j#+!|%45689\\?@^~`$7\r\n\t";

    private readonly byte[] _pattern;
    private readonly int _anchorOffset;
    private readonly byte _anchorLower;
    private readonly byte _anchorUpper;

    public AsciiLiteralIgnoreCaseMatcher(byte[] pattern) {
      if (pattern.Length == 0)
        throw new ArgumentException("Pattern must not be empty", nameof(pattern));
      _pattern = pattern;
      _anchorOffset = GetAnchorOffset(pattern);
      var anchor = (char)pattern[_anchorOffset];
      _anchorLower = (byte)char.ToLowerInvariant(anchor);
      _anchorUpper = (byte)char.ToUpperInvariant(anchor);
    }

    private static int GetAnchorOffset(byte[] pattern) {
      var result = 0;
      var resultRank = int.MinValue;
      for (var i = 0; i < pattern.Length; i++) {
        var index = CharactersByFrequency.IndexOf(char.ToLowerInvariant((char)pattern[i]));
        var rank = (index < 0) ? int.MaxValue : index;
        if (rank > resultRank) {
          result = i;
          resultRank = rank;
        }
      }
      return result;
    }

    public bool FindNext(ReadOnlySpan<byte> text, int start, out int matchIndex, out int matchLength) {
      matchLength = _pattern.Length;
      // The anchor character must be at a position "p" such that the whole
      // pattern fits in the text, i.e. p - anchorOffset + patternLength <= textLength.
      var anchorLimit = text.Length - _pattern.Length + _anchorOffset; // included
      var anchorStart = start + _anchorOffset;
      while (anchorStart <= anchorLimit) {
        var searchSpan = text.Slice(anchorStart, anchorLimit - anchorStart + 1);
        var index = (_anchorLower == _anchorUpper)
          ? searchSpan.IndexOf(_anchorLower)
          : searchSpan.IndexOfAny(_anchorLower, _anchorUpper);
        if (index < 0)
          break;

        var candidate = anchorStart + index - _anchorOffset;
        if (Ascii.EqualsIgnoreCase(text.Slice(candidate, _pattern.Length), _pattern)) {
          matchIndex = candidate;
          return true;
        }
        anchorStart += index + 1;
      }

      matchIndex = -1;
      return false;
    }
  }

  /// <summary>
  /// Search of a literal in UTF-16 text.
  /// </summary>
  public sealed class Utf16LiteralMatcher : ITextMatcher<char> {
    private readonly string _pattern;
    private readonly SearchValues<string> _ignoreCaseSearchValues;

    public Utf16LiteralMatcher(string pattern, bool matchCase) {
      _pattern = pattern;
      if (!matchCase) {
        _ignoreCaseSearchValues = SearchValues.Create(new[] { pattern }, StringComparison.OrdinalIgnoreCase);
      }
    }

    public bool FindNext(ReadOnlySpan<char> text, int start, out int matchIndex, out int matchLength) {
      var span = text.Slice(start);
      int index;
      if (_ignoreCaseSearchValues == null) {
        index = span.IndexOf(_pattern.AsSpan());
        matchLength = _pattern.Length;
      } else {
        index = span.IndexOfAny(_ignoreCaseSearchValues);
        // Note: With "OrdinalIgnoreCase", matches have the same length as the
        // pattern (simple case folding).
        matchLength = _pattern.Length;
      }
      matchIndex = start + index;
      return index >= 0;
    }
  }

  /// <summary>
  /// Regular expression search in UTF-16 text.
  /// </summary>
  public sealed class RegexMatcher : ITextMatcher<char> {
    private readonly Regex _regex;

    public RegexMatcher(Regex regex) {
      _regex = regex;
    }

    public bool FindNext(ReadOnlySpan<char> text, int start, out int matchIndex, out int matchLength) {
      foreach (var match in _regex.EnumerateMatches(text, start)) {
        matchIndex = match.Index;
        matchLength = match.Length;
        return true;
      }
      matchIndex = -1;
      matchLength = 0;
      return false;
    }

    /// <summary>
    /// Create the .NET regular expression used for searching file contents
    /// and file names. We use the "non backtracking" engine, which
    /// guarantees a search time linear in the size of the input (same as the
    /// RE2 engine used before).
    /// </summary>
    public static Regex CreateRegex(string pattern, bool matchCase) {
      var options = RegexOptions.NonBacktracking |
                    RegexOptions.CultureInvariant |
                    RegexOptions.ExplicitCapture |
                    RegexOptions.Multiline;
      if (!matchCase) {
        options |= RegexOptions.IgnoreCase;
      }
      try {
        return new Regex(pattern, options);
      }
      catch (RegexParseException e) {
        throw new RecoverableErrorException(e.Message);
      }
      catch (ArgumentException e) {
        throw new RecoverableErrorException(e.Message);
      }
      catch (NotSupportedException e) {
        throw new RecoverableErrorException(e.Message);
      }
    }
  }

  /// <summary>
  /// Never matches anything (e.g. a non ASCII pattern in ASCII text).
  /// </summary>
  public sealed class NoMatchMatcher<TChar> : ITextMatcher<TChar> where TChar : unmanaged {
    public static readonly NoMatchMatcher<TChar> Instance = new NoMatchMatcher<TChar>();

    public bool FindNext(ReadOnlySpan<TChar> text, int start, out int matchIndex, out int matchLength) {
      matchIndex = -1;
      matchLength = 0;
      return false;
    }
  }
}
