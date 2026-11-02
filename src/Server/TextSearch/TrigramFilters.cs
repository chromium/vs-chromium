// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Prefilter of the code search: for each chunk of text (each "piece" of a file), a Bloom filter
  /// of the trigrams (3 consecutive bytes of the UTF-8 text, ASCII letters folded to lower case)
  /// of the chunk.
  /// A search for a text skips the chunks whose filter doesn't contain all the trigrams of the text
  /// (or of the literals that every match must contain, for wildcards and regular expressions):
  /// such chunks can't contain a match. Bloom filters have no false negative, so results are
  /// unchanged; false positives only mean chunks scanned for nothing.
  ///
  /// The size of each filter is proportional to the number of distinct trigrams of its chunk
  /// (<see cref="BitsPerTrigram"/>), so that the false positive rate is the same for all chunks.
  /// See "Trigram filters" in docs/portable-server/PORTING_PRINCIPLES.md for the memory vs speed
  /// measurements.
  /// </summary>
  public static class TrigramFilters {
    /// <summary>
    /// Bits per distinct trigram of a chunk (0: filters disabled). Environment variable
    /// VSCHROMIUM_TRIGRAM_FILTER_BITS (for benchmarks), default 4: on the large benchmark corpus,
    /// 4 bits (3 hash functions, ~5% of the size of the file contents) filter almost as well as 8
    /// or 12 bits (see PORTING_PRINCIPLES.md).
    /// </summary>
    public static readonly int BitsPerTrigram = GetBitsPerTrigram();

    /// <summary>
    /// Number of hash functions (bits set per trigram): ~ BitsPerTrigram * ln(2).
    /// </summary>
    public static readonly int HashCount = Math.Max(1, (int)Math.Round(BitsPerTrigram * 0.6931));

    public static bool Enabled => BitsPerTrigram > 0;

    private static int GetBitsPerTrigram() {
      var value = Environment.GetEnvironmentVariable("VSCHROMIUM_TRIGRAM_FILTER_BITS");
      int bits;
      if (!string.IsNullOrEmpty(value) && int.TryParse(value, out bits) && bits >= 0 && bits <= 32)
        return bits;
      return 4;
    }

    /// <summary>
    /// The key of a trigram: the 3 (folded) bytes of UTF-8 text.
    /// </summary>
    internal static ulong Key(uint c0, uint c1, uint c2) {
      return ((ulong)Fold(c0) << 32) | ((ulong)Fold(c1) << 16) | Fold(c2);
    }

    internal static uint Fold(uint c) {
      return c >= 'A' && c <= 'Z' ? c | 0x20 : c;
    }

    internal static void Hash(ulong key, out uint h1, out uint h2) {
      var h = key * 0x9E3779B97F4A7C15UL;
      h ^= h >> 29;
      h *= 0xBF58476D1CE4E5B9UL;
      h ^= h >> 32;
      h1 = (uint)(h >> 32);
      h2 = (uint)h | 1;
    }

    /// <summary>
    /// Position of the i-th bit of a trigram in a filter of <paramref name="bitCount"/> bits
    /// (double hashing, multiplicative range reduction: no division).
    /// </summary>
    internal static uint BitPosition(uint h1, uint h2, int i, uint bitCount) {
      return (uint)(((ulong)(h1 + (uint)i * h2) * bitCount) >> 32);
    }

    /// <summary>
    /// The trigrams (of the UTF-8 bytes) that every match of a search must contain (null if there
    /// are none, e.g. a text shorter than 3 bytes: no filtering is possible).
    /// </summary>
    /// <param name="requiredLiterals">The literals every match contains.</param>
    /// <param name="matchCase">False if the search ignores case.</param>
    /// <param name="useRegex">True for regular expressions: when ignoring case, the regular
    /// expression engine matches non ASCII characters that are not equal to the literal with
    /// ASCII case folding (e.g. the Kelvin sign and "k"), so the query can't be used for text with
    /// non ASCII characters.</param>
    public static TrigramQuery CreateQuery(IEnumerable<string> requiredLiterals, bool matchCase, bool useRegex) {
      if (!Enabled)
        return null;
      var keys = new HashSet<ulong>();
      foreach (var literal in requiredLiterals) {
        if (literal == null)
          continue;
        if (!matchCase && !System.Text.Ascii.IsValid(literal)) {
          // Case insensitive search of non ASCII text: Unicode case folding, not the ASCII
          // folding of the filters.
          return null;
        }
        var bytes = Utf8Literals.GetBytes(literal);
        if (bytes == null)
          continue;
        for (var i = 2; i < bytes.Length; i++) {
          keys.Add(Key(bytes[i - 2], bytes[i - 1], bytes[i]));
        }
      }
      if (keys.Count == 0)
        return null;
      return new TrigramQuery(keys, appliesToNonAsciiText: matchCase || !useRegex);
    }
  }

  /// <summary>
  /// The trigrams of a search (see <see cref="TrigramFilters.CreateQuery"/>), with their hashes.
  /// </summary>
  public sealed class TrigramQuery {
    private readonly uint[] _h1;
    private readonly uint[] _h2;

    internal TrigramQuery(ICollection<ulong> keys, bool appliesToNonAsciiText) {
      AppliesToNonAsciiText = appliesToNonAsciiText;
      _h1 = new uint[keys.Count];
      _h2 = new uint[keys.Count];
      var index = 0;
      foreach (var key in keys) {
        TrigramFilters.Hash(key, out _h1[index], out _h2[index]);
        index++;
      }
    }

    /// <summary>
    /// False if the filters of text containing non ASCII characters can't be used (see <see
    /// cref="TrigramFilters.CreateQuery"/>).
    /// </summary>
    public bool AppliesToNonAsciiText { get; }

    internal bool IsContainedIn(ulong[] bits, int start, int wordCount) {
      var bitCount = (uint)(wordCount * 64);
      var k = TrigramFilters.HashCount;
      for (var t = 0; t < _h1.Length; t++) {
        for (var i = 0; i < k; i++) {
          var position = TrigramFilters.BitPosition(_h1[t], _h2[t], i, bitCount);
          if ((bits[start + (int)(position >> 6)] & (1UL << (int)(position & 63))) == 0)
            return false;
        }
      }
      return true;
    }
  }

  /// <summary>
  /// The trigram filters of the chunks of one file contents (immutable).
  /// </summary>
  public sealed class TrigramFilterSet {
    /// <summary>Filters of all the chunks, concatenated.</summary>
    private readonly ulong[] _bits;
    /// <summary>Start (in words) of the filter of each chunk, plus the end (null: single chunk).</summary>
    private readonly int[] _offsets;

    private TrigramFilterSet(ulong[] bits, int[] offsets) {
      _bits = bits;
      _offsets = offsets;
    }

    public long ByteSize => _bits.Length * 8L + (_offsets == null ? 0 : _offsets.Length * 4L);

    /// <summary>
    /// Returns false if the chunk <paramref name="chunkIndex"/> certainly doesn't contain all the
    /// trigrams of <paramref name="query"/>.
    /// </summary>
    public bool MayContain(int chunkIndex, TrigramQuery query) {
      if (_offsets == null)
        return query.IsContainedIn(_bits, 0, _bits.Length);
      var start = _offsets[chunkIndex];
      return query.IsContainedIn(_bits, start, _offsets[chunkIndex + 1] - start);
    }

    [ThreadStatic]
    private static ulong[] _asciiTrigramMap; // 2^24 bits: all the trigrams of 3 bytes
    [ThreadStatic]
    private static List<uint> _asciiTrigrams;

    /// <summary>
    /// Builds the filters of the chunks <paramref name="chunks"/> (ranges of the UTF-8 text <paramref name="text"/>).
    /// </summary>
    public static TrigramFilterSet Build(ReadOnlySpan<byte> text, IReadOnlyList<TextRange> chunks) {
      var map = _asciiTrigramMap ??= new ulong[1 << 18];
      var trigrams = _asciiTrigrams ??= new List<uint>(16 * 1024);
      var filters = new List<ulong[]>(chunks.Count);
      foreach (var chunk in chunks) {
        var span = text.Slice(chunk.Position, chunk.Length);
        trigrams.Clear();
        uint t = 0;
        for (var i = 0; i < span.Length; i++) {
          t = ((t << 8) | TrigramFilters.Fold(span[i])) & 0xFFFFFF;
          if (i >= 2) {
            var word = t >> 6;
            var bit = 1UL << (int)(t & 63);
            if ((map[word] & bit) == 0) {
              map[word] |= bit;
              trigrams.Add(t);
            }
          }
        }
        var filter = AllocateFilter(trigrams.Count);
        foreach (var trigram in trigrams) {
          map[trigram >> 6] = 0;
          // Note: Bytes are already folded
          AddToFilter(filter, ((ulong)(trigram >> 16) << 32) | ((ulong)((trigram >> 8) & 0xFF) << 16) | (trigram & 0xFF));
        }
        filters.Add(filter);
      }
      return Create(filters);
    }

    private static ulong[] AllocateFilter(int trigramCount) {
      var bitCount = Math.Max(64, (long)trigramCount * TrigramFilters.BitsPerTrigram);
      return new ulong[(bitCount + 63) / 64];
    }

    private static void AddToFilter(ulong[] filter, ulong key) {
      TrigramFilters.Hash(key, out var h1, out var h2);
      var bitCount = (uint)(filter.Length * 64);
      for (var i = 0; i < TrigramFilters.HashCount; i++) {
        var position = TrigramFilters.BitPosition(h1, h2, i, bitCount);
        filter[position >> 6] |= 1UL << (int)(position & 63);
      }
    }

    private static TrigramFilterSet Create(List<ulong[]> filters) {
      if (filters.Count == 1)
        return new TrigramFilterSet(filters[0], null);
      var offsets = new int[filters.Count + 1];
      var total = 0;
      for (var i = 0; i < filters.Count; i++) {
        offsets[i] = total;
        total += filters[i].Length;
      }
      offsets[filters.Count] = total;
      var bits = new ulong[total];
      for (var i = 0; i < filters.Count; i++) {
        Array.Copy(filters[i], 0, bits, offsets[i], filters[i].Length);
      }
      return new TrigramFilterSet(bits, offsets);
    }
  }
}
