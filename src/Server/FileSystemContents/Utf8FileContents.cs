// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VsChromium.Core.Memory;
using VsChromium.Server.TextSearch;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// FileContents implementation for text files: the UTF-8 bytes of the file (without the BOM, if
  /// any). Positions (text ranges, chunks, pieces) are byte positions; positions exchanged with
  /// clients (search results, file extracts) are positions in the UTF-16 decoding of the text
  /// (<see cref="Encoding.UTF8"/>, invalid bytes replaced with U+FFFD), i.e. the same as when the
  /// text was stored as UTF-16. For files containing only ASCII characters (<see
  /// cref="IsAsciiText"/>), both are the same.
  /// </summary>
  public class Utf8FileContents : FileContents {
    /// <summary>
    /// Distance (in bytes) between the checkpoints used to convert UTF-8 positions to UTF-16
    /// positions in large non ASCII files (see <see cref="GetCheckpoints"/>).
    /// </summary>
    private const int CheckpointInterval = 64 * 1024;
    private readonly bool _isAscii;
    private Checkpoints _checkpoints;

    public Utf8FileContents(FileContentsMemory contents, DateTime utcLastModified, bool isAscii)
      : base(contents, utcLastModified) {
      _isAscii = isAscii;
    }

    /// <summary>
    /// True if the text contains only ASCII characters (bytes less than 0x80).
    /// </summary>
    protected override bool IsAsciiText => _isAscii;

    /// <summary>
    /// True if the text contains only ASCII characters (bytes less than 0x80).
    /// </summary>
    public bool IsAscii => _isAscii;

    private unsafe ReadOnlySpan<byte> Bytes => new ReadOnlySpan<byte>(Contents.Pointer.ToPointer(), ByteLength);

    protected override ITextLineOffsets GetFileOffsets() {
      return new AsciiTextLineOffsets(Contents);
    }

    public override IEnumerable<FileExtract> GetFileExtracts(int maxLength, IEnumerable<FilePositionSpan> spans) {
      if (_isAscii)
        return base.GetFileExtracts(maxLength, spans);

      // Positions are UTF-16 positions: compute the extracts from the UTF-16 decoding of the text.
      var bytes = Bytes;
      var charCount = Encoding.UTF8.GetCharCount(bytes);
      using (var block = NativeHeap.Alloc(Math.Max(1, charCount) * sizeof(char))) {
        unsafe {
          Encoding.UTF8.GetChars(bytes, new Span<char>(block.Pointer.ToPointer(), charCount));
        }
        var offsets = new TextLineOffsetsImpl(new FileContentsMemory(block, 0, charCount * sizeof(char)), sizeof(char));
        offsets.CollectLineOffsets();
        return spans
          .Select(x => offsets.FilePositionSpanToFileExtract(x, maxLength))
          .Where(x => x != null)
          .ToList();
      }
    }

    protected override TrigramFilterSet CreateTrigramFilters(IReadOnlyList<TextRange> chunks) {
      return TrigramFilterSet.Build(Bytes, chunks);
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container) {
      return _isAscii ? container.GetAsciiSearch() : container.GetUtf8Search();
    }

    protected override TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      return LineExtents.GetLineExtentFromPosition(Bytes, position, MaxLineExtentOffset, (byte)'\n');
    }

    protected override int LastIndexOfNewLine(TextRange range) {
      var index = Bytes.Slice(range.Position, range.Length).LastIndexOf((byte)'\n');
      return index < 0 ? index : range.Position + index;
    }

    protected override int GetCharacterBoundary(int position) {
      if (_isAscii)
        return position;
      // A byte that is not a continuation byte (10xxxxxx) starts a character (or an invalid
      // sequence) in the UTF-8 decoding of the whole text.
      var bytes = Bytes;
      var result = position;
      for (var i = 0; i < 3 && result > 0 && result < bytes.Length && (bytes[result] & 0xC0) == 0x80; i++)
        result--;
      // Invalid sequence of more than 3 continuation bytes: no character boundary nearby.
      return (result < bytes.Length && (bytes[result] & 0xC0) == 0x80) ? position : result;
    }

    protected override IList<TextRange> ToUtf16Ranges(IList<TextRange> ranges) {
      if (_isAscii || ranges.Count == 0)
        return ranges;

      var bytes = Bytes;
      var checkpoints = bytes.Length > 2 * CheckpointInterval ? GetCheckpoints() : null;
      var result = new List<TextRange>(ranges.Count);
      var bytePosition = 0;
      var charPosition = 0;
      foreach (var range in ranges) {
        if (range.Position < bytePosition) {
          bytePosition = 0;
          charPosition = 0;
        }
        if (checkpoints != null) {
          checkpoints.Find(range.Position, ref bytePosition, ref charPosition);
        }
        charPosition += Encoding.UTF8.GetCharCount(bytes.Slice(bytePosition, range.Position - bytePosition));
        bytePosition = range.Position;
        var charLength = Encoding.UTF8.GetCharCount(bytes.Slice(range.Position, range.Length));
        result.Add(new TextRange(charPosition, charLength));
      }
      return result;
    }

    /// <summary>
    /// UTF-16 positions of byte positions at regular intervals (character boundaries), so that
    /// converting a byte position to a UTF-16 position doesn't need to decode the text from its
    /// beginning. Computed on first use (search results in a large non ASCII file).
    /// </summary>
    private Checkpoints GetCheckpoints() {
      var result = _checkpoints;
      if (result == null) {
        result = Checkpoints.Create(Bytes, CheckpointInterval);
        Interlocked.CompareExchange(ref _checkpoints, result, null);
      }
      return result;
    }

    private sealed class Checkpoints {
      private readonly int[] _bytePositions;
      private readonly int[] _charPositions;

      private Checkpoints(int[] bytePositions, int[] charPositions) {
        _bytePositions = bytePositions;
        _charPositions = charPositions;
      }

      public static Checkpoints Create(ReadOnlySpan<byte> bytes, int interval) {
        var bytePositions = new List<int>();
        var charPositions = new List<int>();
        var bytePosition = 0;
        var charPosition = 0;
        for (var next = interval; next < bytes.Length; next += interval) {
          // Move to a character boundary (see GetCharacterBoundary)
          var boundary = next;
          while (boundary < bytes.Length && (bytes[boundary] & 0xC0) == 0x80)
            boundary++;
          if (boundary >= bytes.Length)
            break;
          charPosition += Encoding.UTF8.GetCharCount(bytes.Slice(bytePosition, boundary - bytePosition));
          bytePosition = boundary;
          bytePositions.Add(bytePosition);
          charPositions.Add(charPosition);
          next = Math.Max(next, boundary);
        }
        return new Checkpoints(bytePositions.ToArray(), charPositions.ToArray());
      }

      /// <summary>
      /// Moves (<paramref name="bytePosition"/>, <paramref name="charPosition"/>) to the last
      /// checkpoint before <paramref name="position"/>, if it is after <paramref
      /// name="bytePosition"/>.
      /// </summary>
      public void Find(int position, ref int bytePosition, ref int charPosition) {
        var index = Array.BinarySearch(_bytePositions, position);
        if (index < 0)
          index = ~index - 1;
        if (index >= 0 && _bytePositions[index] > bytePosition) {
          bytePosition = _bytePositions[index];
          charPosition = _charPositions[index];
        }
      }
    }
  }
}
