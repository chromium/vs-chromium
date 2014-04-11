// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    private const int _maxTextExtent = 50;
    private readonly FileContentsMemory _heap;

    public AsciiFileContents(FileContentsMemory heap, DateTime utcLastWriteTime)
      : base(utcLastWriteTime) {
      _heap = heap;
    }

    public override long ByteLength { get { return _heap.ContentsByteLength; } }
    private IntPtr Pointer { get { return _heap.ContentsPointer; } }
    private long CharacterCount { get { return _heap.ContentsByteLength; } }

    public static AsciiStringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (pattern.Length <= 64)
        return new AsciiStringSearchBndm64(pattern, searchOptions);
      else
        return new AsciiStringSearchBoyerMoore(pattern, searchOptions);
    }

    public override List<FilePositionSpan> Search(SearchContentsData searchContentsData) {
      if (searchContentsData.ParsedSearchString.MainEntry.Text.Length > ByteLength)
        return NoSpans;

      var algo = searchContentsData.ParsedSearchString.MainEntry.AsciiStringSearchAlgo;
      // TODO(rpaquay): We are limited to 2GB for now.
      var result = algo.SearchAll(Pointer, (int)ByteLength);
      if (searchContentsData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          searchContentsData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(searchContentsData.ParsedSearchString, result).ToList();
    }

    private IEnumerable<FilePositionSpan> FilterOnOtherEntries(ParsedSearchString parsedSearchString, IEnumerable<FilePositionSpan> matches) {
      FindEntry findEntry = (position, length, entry) => {
        var start = Pointers.AddPtr(this.Pointer, position);
        var result = entry.AsciiStringSearchAlgo.Search(start, length);
        if (result == IntPtr.Zero)
          return -1;
        return position + Pointers.Offset32(start, result);
      };
      Func<int, FilePositionSpan> getLineExtent = position => {
        int lineStart;
        int lineLength;
        NativeMethods.Ascii_GetLineExtentFromPosition(this.Pointer, (int)this.CharacterCount, position, out lineStart, out lineLength);
        return new FilePositionSpan() {Position = lineStart, Length = lineLength};
      };

      return new TextSourceTextSearch(getLineExtent, findEntry).FilterOnOtherEntries(parsedSearchString, matches);
    }

    public override IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      var offsets = new AsciiTextLineOffsets(_heap);
      offsets.CollectLineOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, _maxTextExtent))
        .Where(x => x != null)
        .ToList();
    }
  }
}
