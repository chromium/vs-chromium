// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Search of a literal in ASCII text, case sensitive or not. Candidate
  /// positions are found by comparing (case insensitively if needed) the two
  /// least frequent
  /// characters of the pattern (as estimated for source code) at their
  /// respective offsets, 16 or 32 positions at a time with SIMD instructions
  /// (the "generic SIMD" substring search algorithm, with case folding); the
  /// whole pattern is then compared at each candidate position. Choosing rare
  /// characters (instead of the first and last characters, as
  /// Span.IndexOf does) minimizes the number of candidate positions.
  /// </summary>
  public sealed class AsciiLiteralMatcher : ITextMatcher<byte> {
    // ASCII characters ordered by decreasing (approximate) frequency in source
    // code, letters in lower case. Characters not in this list are considered
    // the least frequent.
    private const string CharactersByFrequency =
      " etainorslcdu_hpm(f)gb.;,=v*/kwyx-\"0>1<{}:[]2&qz'3j#+!|%45689\\?@^~`$7\r\n\t";

    private const byte CaseBit = 0x20;

    private readonly byte[] _pattern;
    private readonly bool _matchCase;
    private readonly int _offset1;
    private readonly int _offset2;
    private readonly int _maxOffset;
    // Value of the anchor characters with the case bit set (for letters), and
    // the mask to apply to text characters before comparing (CaseBit for
    // letters, 0 for other characters).
    private readonly byte _anchor1;
    private readonly byte _mask1;
    private readonly byte _anchor2;
    private readonly byte _mask2;

    public AsciiLiteralMatcher(byte[] pattern, bool matchCase) {
      if (pattern.Length == 0)
        throw new ArgumentException("Pattern must not be empty", nameof(pattern));
      _pattern = pattern;
      _matchCase = matchCase;
      ChooseAnchors(pattern, out _offset1, out _offset2);
      _maxOffset = Math.Max(_offset1, _offset2);
      GetAnchor(pattern[_offset1], matchCase, out _anchor1, out _mask1);
      GetAnchor(pattern[_offset2], matchCase, out _anchor2, out _mask2);
    }

    private static void GetAnchor(byte ch, bool matchCase, out byte anchor, out byte mask) {
      var isLetter = !matchCase && ((ch >= 'a' && ch <= 'z') || (ch >= 'A' && ch <= 'Z'));
      mask = isLetter ? CaseBit : (byte)0;
      anchor = (byte)(ch | mask);
    }

    private static int GetRank(byte ch) {
      var index = CharactersByFrequency.IndexOf(char.ToLowerInvariant((char)ch));
      return (index < 0) ? int.MaxValue : index;
    }

    /// <summary>
    /// Choose the offsets of the two least frequent characters of the
    /// pattern (the same offset if the pattern has a single character).
    /// </summary>
    private static void ChooseAnchors(byte[] pattern, out int offset1, out int offset2) {
      offset1 = 0;
      for (var i = 1; i < pattern.Length; i++) {
        if (GetRank(pattern[i]) > GetRank(pattern[offset1]))
          offset1 = i;
      }
      offset2 = offset1;
      for (var i = 0; i < pattern.Length; i++) {
        if (i == offset1)
          continue;
        if (offset2 == offset1 || GetRank(pattern[i]) > GetRank(pattern[offset2]))
          offset2 = i;
      }
    }

    private bool IsMatch(ReadOnlySpan<byte> text) {
      return _matchCase ? text.SequenceEqual(_pattern) : Ascii.EqualsIgnoreCase(text, _pattern);
    }

    public bool FindNext(ReadOnlySpan<byte> text, int start, out int matchIndex, out int matchLength) {
      matchLength = _pattern.Length;
      // Last position where the pattern fits in the text (included).
      var lastStart = text.Length - _pattern.Length;
      var index = start;

      if (Vector256.IsHardwareAccelerated) {
        if (FindVector256(text, ref index, lastStart, out matchIndex))
          return true;
      } else if (Vector128.IsHardwareAccelerated) {
        if (FindVector128(text, ref index, lastStart, out matchIndex))
          return true;
      }

      // Scalar search of the remaining positions.
      for (; index <= lastStart; index++) {
        if ((text[index + _offset1] | _mask1) == _anchor1 &&
            (text[index + _offset2] | _mask2) == _anchor2 &&
            IsMatch(text.Slice(index, _pattern.Length))) {
          matchIndex = index;
          return true;
        }
      }

      matchIndex = -1;
      return false;
    }

    /// <summary>
    /// Search 32 positions at a time while the vector loads stay within the
    /// text. On return, <paramref name="index"/> is the first position not
    /// examined yet (if there is no match).
    /// </summary>
    private bool FindVector256(ReadOnlySpan<byte> text, ref int index, int lastStart, out int matchIndex) {
      ref var textRef = ref MemoryMarshal.GetReference(text);
      var anchor1 = Vector256.Create(_anchor1);
      var mask1 = Vector256.Create(_mask1);
      var anchor2 = Vector256.Create(_anchor2);
      var mask2 = Vector256.Create(_mask2);
      // Last position (included) for which both vector loads are in bounds.
      var vectorLimit = text.Length - Vector256<byte>.Count - _maxOffset;
      // Note: Copy fields to locals, so that they stay in registers.
      var offset1 = (nuint)_offset1;
      var offset2 = (nuint)_offset2;
      for (; index <= vectorLimit; index += Vector256<byte>.Count) {
        var chars1 = Vector256.LoadUnsafe(ref textRef, (nuint)index + offset1) | mask1;
        var chars2 = Vector256.LoadUnsafe(ref textRef, (nuint)index + offset2) | mask2;
        var matches = Vector256.Equals(chars1, anchor1) & Vector256.Equals(chars2, anchor2);
        if (matches == Vector256<byte>.Zero)
          continue;
        var bits = matches.ExtractMostSignificantBits();
        while (bits != 0) {
          var candidate = index + BitOperations.TrailingZeroCount(bits);
          if (candidate <= lastStart && IsMatch(text.Slice(candidate, _pattern.Length))) {
            matchIndex = candidate;
            return true;
          }
          bits &= bits - 1;
        }
      }
      matchIndex = -1;
      return false;
    }

    /// <summary>
    /// Same as <see cref="FindVector256"/>, 16 positions at a time.
    /// </summary>
    private bool FindVector128(ReadOnlySpan<byte> text, ref int index, int lastStart, out int matchIndex) {
      ref var textRef = ref MemoryMarshal.GetReference(text);
      var anchor1 = Vector128.Create(_anchor1);
      var mask1 = Vector128.Create(_mask1);
      var anchor2 = Vector128.Create(_anchor2);
      var mask2 = Vector128.Create(_mask2);
      var vectorLimit = text.Length - Vector128<byte>.Count - _maxOffset;
      // Note: Copy fields to locals, so that they stay in registers.
      var offset1 = (nuint)_offset1;
      var offset2 = (nuint)_offset2;
      for (; index <= vectorLimit; index += Vector128<byte>.Count) {
        var chars1 = Vector128.LoadUnsafe(ref textRef, (nuint)index + offset1) | mask1;
        var chars2 = Vector128.LoadUnsafe(ref textRef, (nuint)index + offset2) | mask2;
        var matches = Vector128.Equals(chars1, anchor1) & Vector128.Equals(chars2, anchor2);
        if (matches == Vector128<byte>.Zero)
          continue;
        var bits = matches.ExtractMostSignificantBits();
        while (bits != 0) {
          var candidate = index + BitOperations.TrailingZeroCount(bits);
          if (candidate <= lastStart && IsMatch(text.Slice(candidate, _pattern.Length))) {
            matchIndex = candidate;
            return true;
          }
          bits &= bits - 1;
        }
      }
      matchIndex = -1;
      return false;
    }
  }
}
