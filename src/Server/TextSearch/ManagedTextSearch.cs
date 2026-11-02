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
        var postMatchRange = postProcess(new TextRange(textPosition + matchIndex, matchLength));
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
      if (index > 0 && IsWordCharacter(text[index - 1]))
        return false;
      var end = index + length;
      if (end < text.Length && IsWordCharacter(text[end]))
        return false;
      return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsWordCharacter<TChar>(TChar ch) where TChar : unmanaged {
      if (typeof(TChar) == typeof(byte))
        return WordCharacters.IsWordCharacter((char)Unsafe.As<TChar, byte>(ref ch));
      return WordCharacters.IsWordCharacter(Unsafe.As<TChar, char>(ref ch));
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
  }
}
