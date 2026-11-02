// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using VsChromium.Core.Utility;
using VsChromium.Server.TextSearch;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Finds the next match of a search pattern in a span of characters (bytes
  /// for ASCII text, chars for UTF-16 text).
  /// </summary>
  public interface ITextMatcher<TChar> where TChar : unmanaged {
    /// <summary>
    /// Look for the first match starting at index <paramref name="start"/>
    /// (included) of <paramref name="text"/>. Returns <code>false</code> if
    /// there is no match.
    /// </summary>
    bool FindNext(ReadOnlySpan<TChar> text, int start, out int matchIndex, out int matchLength);
  }

  /// <summary>
  /// Base class of the managed implementations of <see
  /// cref="ICompiledTextSearch"/>: enumerates matches (with the "whole word"
  /// option applied as a filter), converts them to text ranges and applies
  /// the caller post processing.
  /// </summary>
  public abstract class ManagedTextSearch : ICompiledTextSearch {
    private static readonly List<TextRange> NoResult = new List<TextRange>();
    private static readonly Func<TextRange, TextRange?> Identity = x => x;
    private readonly bool _matchWholeWord;

    protected ManagedTextSearch(bool matchWholeWord) {
      _matchWholeWord = matchWholeWord;
    }

    public virtual void Dispose() {
    }

    public IList<TextRange> FindAll(TextFragment textFragment, Func<TextRange, TextRange?> postProcess, IOperationProgressTracker progressTracker) {
      if (progressTracker.ShouldEndProcessing)
        return NoResult;
      return Find(textFragment, postProcess, progressTracker, int.MaxValue) ?? NoResult;
    }

    public TextRange? FindFirst(TextFragment textFragment, IOperationProgressTracker progressTracker) {
      var result = Find(textFragment, Identity, progressTracker, 1);
      if (result == null || result.Count == 0)
        return null;
      return result[0];
    }

    /// <summary>
    /// Find up to <paramref name="maxResultSize"/> matches in <paramref
    /// name="textFragment"/>. Returns <code>null</code> if there are no
    /// matches.
    /// </summary>
    protected abstract List<TextRange> Find(
      TextFragment textFragment,
      Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker,
      int maxResultSize);

    protected static unsafe ReadOnlySpan<TChar> GetSpan<TChar>(TextFragment textFragment) where TChar : unmanaged {
      if (textFragment.CharacterSize != sizeof(TChar)) {
        throw new ArgumentException(
          string.Format("Text fragment character size ({0}) is not supported by this search algorithm ({1})",
            textFragment.CharacterSize, sizeof(TChar)), nameof(textFragment));
      }
      return new ReadOnlySpan<TChar>(textFragment.StartPtr.ToPointer(), textFragment.Length);
    }

    /// <summary>
    /// Enumerates the matches of <paramref name="matcher"/> in <paramref
    /// name="text"/>. Match positions are relative to the beginning of the
    /// text, i.e. <paramref name="textPosition"/> is added to the position in
    /// <paramref name="text"/>.
    /// </summary>
    protected List<TextRange> FindMatches<TChar>(
      ReadOnlySpan<TChar> text,
      int textPosition,
      ITextMatcher<TChar> matcher,
      Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker,
      int maxResultSize) where TChar : unmanaged {
      var mapper = new IdentityRangeMapper();
      return FindMatches(text, textPosition, matcher, ref mapper, postProcess, progressTracker, maxResultSize);
    }

    /// <summary>
    /// Same as above, with the positions of the matches in <paramref name="text"/> converted to
    /// positions in the text fragment by <paramref name="mapper"/> (e.g. when <paramref
    /// name="text"/> is the UTF-16 decoding of a UTF-8 text fragment).
    /// </summary>
    protected List<TextRange> FindMatches<TChar, TMapper>(
      ReadOnlySpan<TChar> text,
      int textPosition,
      ITextMatcher<TChar> matcher,
      ref TMapper mapper,
      Func<TextRange, TextRange?> postProcess,
      IOperationProgressTracker progressTracker,
      int maxResultSize) where TChar : unmanaged where TMapper : IMatchRangeMapper, allows ref struct {
      List<TextRange> result = null;

      var start = 0;
      while (start <= text.Length) {
        if (!matcher.FindNext(text, start, out var matchIndex, out var matchLength))
          break;

        // Same as the native RE2 search: report empty matches (e.g. "^") as
        // matches of length 1, so they can be shown to the user.
        if (matchLength == 0 && matchIndex < text.Length)
          matchLength = 1;
        // Matches don't overlap: the next search starts after this match.
        start = matchIndex + Math.Max(matchLength, 1);

        if (_matchWholeWord && !IsWholeWordMatch(text, matchIndex, matchLength))
          continue;

        // Post process match, maybe skipping it
        var postMatchRange = postProcess(mapper.Map(textPosition, matchIndex, matchLength));
        if (postMatchRange == null)
          continue;

        // Add to result collection
        if (result == null)
          result = new List<TextRange>();
        result.Add(postMatchRange.Value);

        // Check it is time to end processing early.
        maxResultSize--;
        progressTracker.AddResults(1);
        if (maxResultSize <= 0 || progressTracker.ShouldEndProcessing)
          break;
      }

      return result;
    }

    private static bool IsWholeWordMatch<TChar>(ReadOnlySpan<TChar> text, int index, int length) where TChar : unmanaged {
      if (typeof(TChar) == typeof(byte)) {
        var bytes = System.Runtime.InteropServices.MemoryMarshal.Cast<TChar, byte>(text);
        return IsWholeWordMatchUtf8(bytes, index, length);
      }
      var chars = System.Runtime.InteropServices.MemoryMarshal.Cast<TChar, char>(text);
      if (index > 0 && WordCharacters.IsWordCharacter(chars[index - 1]))
        return false;
      var end = index + length;
      if (end < chars.Length && WordCharacters.IsWordCharacter(chars[end]))
        return false;
      return true;
    }

    /// <summary>
    /// Whole word check in UTF-8 text: the characters before and after the match are decoded,
    /// with the same result as in the UTF-16 decoding of the text (see <see
    /// cref="WordCharacters.IsWordCharacter(Rune)"/>).
    /// </summary>
    private static bool IsWholeWordMatchUtf8(ReadOnlySpan<byte> text, int index, int length) {
      if (index > 0) {
        var previous = text[index - 1];
        if (previous < 0x80) {
          if (WordCharacters.IsWordCharacter((char)previous))
            return false;
        } else {
          System.Text.Rune.DecodeLastFromUtf8(text.Slice(0, index), out var rune, out _);
          if (WordCharacters.IsWordCharacter(rune))
            return false;
        }
      }
      var end = index + length;
      if (end < text.Length) {
        var next = text[end];
        if (next < 0x80) {
          if (WordCharacters.IsWordCharacter((char)next))
            return false;
        } else {
          System.Text.Rune.DecodeFromUtf8(text.Slice(end), out var rune, out _);
          if (WordCharacters.IsWordCharacter(rune))
            return false;
        }
      }
      return true;
    }
  }

  /// <summary>
  /// Converts the position of a match in the searched text to a range of the text fragment.
  /// Called for matches in increasing positions.
  /// </summary>
  public interface IMatchRangeMapper {
    TextRange Map(int textPosition, int matchIndex, int matchLength);
  }

  public struct IdentityRangeMapper : IMatchRangeMapper {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TextRange Map(int textPosition, int matchIndex, int matchLength) {
      return new TextRange(textPosition + matchIndex, matchLength);
    }
  }

  /// <summary>
  /// Converts positions in the UTF-16 decoding of a UTF-8 text (<see cref="Encoding.UTF8"/>,
  /// invalid bytes replaced with U+FFFD) to positions in the UTF-8 text, walking the UTF-8 text
  /// forward (positions must be increasing).
  /// </summary>
  public ref struct Utf16ToUtf8RangeMapper : IMatchRangeMapper {
    private readonly ReadOnlySpan<byte> _bytes;
    private int _bytePosition;
    private int _charPosition;

    public Utf16ToUtf8RangeMapper(ReadOnlySpan<byte> bytes) {
      _bytes = bytes;
      _bytePosition = 0;
      _charPosition = 0;
    }

    public TextRange Map(int textPosition, int matchIndex, int matchLength) {
      var start = Advance(matchIndex, roundUp: false);
      var end = Advance(matchIndex + matchLength, roundUp: true);
      return new TextRange(textPosition + start, end - start);
    }

    /// <summary>
    /// Advances to the UTF-16 position <paramref name="charPosition"/> and returns the
    /// corresponding UTF-8 position. A position in the middle of a surrogate pair is rounded
    /// down (start of a match) or up (end of a match) to a character boundary.
    /// </summary>
    private int Advance(int charPosition, bool roundUp) {
      while (_charPosition < charPosition && _bytePosition < _bytes.Length) {
        // Fast path: run of ASCII characters (1 byte = 1 char)
        var maxCount = Math.Min(charPosition - _charPosition, _bytes.Length - _bytePosition);
        var asciiCount = _bytes.Slice(_bytePosition, maxCount).IndexOfAnyInRange((byte)0x80, (byte)0xFF);
        if (asciiCount < 0)
          asciiCount = maxCount;
        _bytePosition += asciiCount;
        _charPosition += asciiCount;
        if (_charPosition >= charPosition || _bytePosition >= _bytes.Length)
          break;

        System.Text.Rune.DecodeFromUtf8(_bytes.Slice(_bytePosition), out var rune, out var bytesConsumed);
        var charCount = rune.Utf16SequenceLength;
        if (_charPosition + charCount > charPosition && !roundUp)
          break;
        _bytePosition += bytesConsumed;
        _charPosition += charCount;
      }
      return _bytePosition;
    }
  }

  public static class WordCharacters {
    /// <summary>
    /// Returns <code>true</code> if <paramref name="ch"/> is part of a
    /// "word" for the "match whole word" option: letters, digits and '_'.
    /// </summary>
    public static bool IsWordCharacter(char ch) {
      if (ch < 128) {
        return (ch >= 'a' && ch <= 'z') ||
               (ch >= 'A' && ch <= 'Z') ||
               (ch >= '0' && ch <= '9') ||
               (ch == '_');
      }
      return char.IsLetterOrDigit(ch);
    }

    /// <summary>
    /// Same as <see cref="IsWordCharacter(char)"/> for a decoded character: characters outside
    /// of the BMP (surrogate pairs in UTF-16 text) are not word characters.
    /// </summary>
    public static bool IsWordCharacter(System.Text.Rune rune) {
      return rune.IsBmp && IsWordCharacter((char)rune.Value);
    }
  }
}
