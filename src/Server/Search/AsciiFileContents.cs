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
    private IntPtr Pointer { get { return _heap.Pointer + _textOffset; } }
    private int CharacterCount { get { return (int)this.ByteLength; } }

    public static AsciiStringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (pattern.Length <= 64)
        return new AsciiStringSearchBndm64(pattern, searchOptions);
      else
        return new AsciiStringSearchBoyerMoore(pattern, searchOptions);
    }

    public override List<FilePositionSpan> Search(SearchContentsData searchContentsData) {
      if (searchContentsData.ParsedSearchString.MainEntry.Text.Length > ByteLength)
        return NoSpans;

      var algo = searchContentsData.AsciiStringSearchAlgo;
      // TODO(rpaquay): We are limited to 2GB for now.
      var result = algo.SearchAll(Pointer, (int)ByteLength);
      if (searchContentsData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          searchContentsData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(searchContentsData.ParsedSearchString, algo.MatchCase, result).ToList();
    }

    private unsafe IEnumerable<FilePositionSpan> FilterOnOtherEntries(ParsedSearchString parsedSearchString, bool matchCase, IEnumerable<FilePositionSpan> matches) {
      byte* start = Pointers.Add(this.Pointer, 0);
      Func<int, char> getCharacter = position => (char)*(start + position);
      return new TextSourceTextSearch(this.CharacterCount, getCharacter).FilterOnOtherEntries(parsedSearchString, matchCase, matches);
    }

    public unsafe char GetCharacterAt(int position) {
      Debug.Assert(position >= 0);
      Debug.Assert(position < this.ByteLength);
      var c = *Pointers.Add(this.Pointer, position);
      return (char)c;
    }

    public override IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      return GetFileExtractsWorker(spans);
    }

    public unsafe IEnumerable<FileExtract> GetFileExtractsWorker(IEnumerable<FilePositionSpan> spans) {
      var blockStart = Pointers.Add(this.Pointer, 0);
      var blockEnd = Pointers.Add(this.Pointer, this.ByteLength);
      var offsets = new AsciiTextLineOffsets(_heap, blockStart, blockEnd);
      offsets.CollectLineOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, _maxTextExtent))
        .Where(x => x != null)
        .ToList();
    }
  }
}
