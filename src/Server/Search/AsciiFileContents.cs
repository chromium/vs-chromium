// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    private const int _maxTextExtent = 50;
    private readonly SafeHeapBlockHandle _heap;
    private readonly int _textOffset;

    public AsciiFileContents(SafeHeapBlockHandle heap, int textOffset, DateTime utcLastWriteTime)
      : base(utcLastWriteTime) {
      if (textOffset > heap.ByteLength)
        throw new ArgumentException("Text offset is too far in buffer", "textOffset");
      _heap = heap;
      _textOffset = textOffset;
    }

    public override long ByteLength { get { return _heap.ByteLength - _textOffset; } }

    public IntPtr Pointer { get { return _heap.Pointer + _textOffset; } }

    public static AsciiStringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (pattern.Length <= 64)
        return new AsciiStringSearchBndm64(pattern, searchOptions);
      else
        return new AsciiStringSearchBoyerMoore(pattern, searchOptions);
    }

    public override List<FilePositionSpan> Search(SearchContentsData searchContentsData) {
      if (searchContentsData.Text.Length > ByteLength)
        return NoSpans;

      // TODO(rpaquay): We are limited to 2GB for now.
      var algo = searchContentsData.AsciiStringSearchAlgo;
      var result = algo.SearchAll(Pointer, (int)ByteLength);
      if (searchContentsData.ParsedSearchString.OtherEntries.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(searchContentsData.ParsedSearchString, algo.MatchCase, result).ToList();
    }

    private unsafe IEnumerable<FilePositionSpan> FilterOnOtherEntries(ParsedSearchString parsedSearchString, bool matchCase, IEnumerable<FilePositionSpan> matches) {
      var blockStart = Pointers.Add(_heap.Pointer, _textOffset);
      var blockEnd = Pointers.Add(_heap.Pointer, _heap.ByteLength);
      var offsets = new AsciiTextLineOffsets(_heap, blockStart, blockEnd);
      offsets.CollectLineOffsets();

      return matches
        .Select(match => {
          var lineExtent = offsets.GetLineExtent(match.Position);
          // We got the line extent, the offset at which we found the MainEntry.
          // Now we need to check that "OtherEntries" are present (in order) and
          // in appropriate intervals.
          var positionInterval1 = lineExtent.Position;
          var lengthInterval1 = match.Position - lineExtent.Position;
          var entriesInterval1 = parsedSearchString.OtherEntries.Where(e => e.Index < parsedSearchString.MainEntry.Index);
          var positionInterval2 = match.Position + match.Length;
          var lengthInterval2 = (lineExtent.Position + lineExtent.Length) - (match.Position + match.Length);
          var entriesInterval2 = parsedSearchString.OtherEntries.Where(e => e.Index > parsedSearchString.MainEntry.Index);

          int foundPositionInterval1;
          int foundLengthInterval1;
          int foundPositionInterval2;
          int foundLengthInterval2;
          if (CheckEntriesInInterval(positionInterval1, lengthInterval1, entriesInterval1, matchCase, out foundPositionInterval1, out foundLengthInterval1) &&
              CheckEntriesInInterval(positionInterval2, lengthInterval2, entriesInterval2, matchCase, out foundPositionInterval2, out foundLengthInterval2)) {

            // If there was no entries before MainEntry, adjust interval
            // location to be the same as the main entry.
            if (foundPositionInterval1 < 0) {
              foundPositionInterval1 = match.Position;
              foundLengthInterval1 = match.Length;
            }
            // If there was no entries after MainEntry, adjust interval to be
            // the same as the main entry.
            if (foundPositionInterval2 < 0) {
              foundPositionInterval2 = match.Position;
              foundLengthInterval2 = match.Length;
            }
            return new FilePositionSpan {
              Position = foundPositionInterval1,
              Length = foundPositionInterval2 + foundLengthInterval2 - foundPositionInterval1
            };
          }
          return new FilePositionSpan();
        })
        .Where(x => x.Length > 0)
        .ToList();
    }

    private bool CheckEntriesInInterval(int position, int length, IEnumerable<ParsedSearchString.Entry> entries, bool matchCase, out int foundPosition, out int foundLength) {
      foundPosition = -1;
      foundLength = 0;
      foreach (var entry in entries) {
        var offset = FindEntry(position, length, entry.Text, matchCase);
        if (offset < 0) {
          foundPosition = -1;
          foundLength = 0;
          return false;
        }

        int advance = offset - position;
        position += advance;
        length -= advance;

        if (foundPosition == -1) {
          foundPosition = offset;
          foundLength = entry.Text.Length;
        } else {
          foundLength = (offset + entry.Text.Length) - foundPosition;
        }
      }
      return true;
    }

    private int FindEntry(int position, int length, string text, bool matchCase) {
      int compareCount = length - text.Length;
      for (var i = 0; i < compareCount; i++) {
        if (MatchEntryText(position + i, text, matchCase))
          return position + i;
      }
      return -1;
    }

    private bool MatchEntryText(int position, string text, bool matchCase) {
      for (var i = 0; i < text.Length; i++) {
        var ch1 = GetCharacterAt(position + i);
        var ch2 = text[i];
        if (!matchCase) {
          ch1 = char.ToLowerInvariant(ch1);
          ch2 = char.ToLowerInvariant(ch2);
        }

        if (ch1 != ch2)
            return false;
      }
      return true;
    }

    private unsafe char GetCharacterAt(int position) {
      Debug.Assert(position >= 0);
      Debug.Assert(position < this.ByteLength - _textOffset);
      var c = *Pointers.Add(_heap.Pointer, _textOffset + position);
      return (char)c;
    }

    public override IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      return GetFileExtractsWorker(spans);
    }

    public unsafe IEnumerable<FileExtract> GetFileExtractsWorker(IEnumerable<FilePositionSpan> spans) {
      var blockStart = Pointers.Add(_heap.Pointer, _textOffset);
      var blockEnd = Pointers.Add(_heap.Pointer, _heap.ByteLength);
      var offsets = new AsciiTextLineOffsets(_heap, blockStart, blockEnd);
      offsets.CollectLineOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, _maxTextExtent))
        .Where(x => x != null)
        .ToList();
    }
  }
}
