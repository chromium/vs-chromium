// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class UTF16FileContents : FileContents {
    private readonly FileContentsMemory _heap;

    public UTF16FileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(utcLastModified) {
      _heap = heap;
    }

    public override long ByteLength { get { return _heap.ByteLength; } }

    private IntPtr Pointer { get { return _heap.Pointer; } }
    private long CharacterCount { get { return _heap.ByteLength / 2; } }

    public static UTF16StringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      return new StrStrWStringSearchAlgorithm(pattern, searchOptions);
    }

    public override List<FilePositionSpan> Search(SearchContentsData searchContentsData) {
      if (searchContentsData.ParsedSearchString.MainEntry.Text.Length > ByteLength)
        return NoSpans;

      var algo = searchContentsData.GetSearchAlgorithms(searchContentsData.ParsedSearchString.MainEntry).UTF16StringSearchAlgo;
      // TODO(rpaquay): We are limited to 2GB for now.
      var result = algo.SearchAll(_heap.Pointer, checked((int)_heap.ByteLength));
      if (searchContentsData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          searchContentsData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(searchContentsData, result).ToList();
    }

    private IEnumerable<FilePositionSpan> FilterOnOtherEntries(SearchContentsData searchContentsData, IEnumerable<FilePositionSpan> matches) {
      FindEntryFunction findEntry = (position, length, entry) => {
        var algo = searchContentsData.GetSearchAlgorithms(searchContentsData.ParsedSearchString.MainEntry).UTF16StringSearchAlgo;
        // TODO(rpaquay): Do we need to take into account sizeof(char) == 2?
        var start = Pointers.AddPtr(this.Pointer, position);
        var result = algo.Search(start, length);
        if (result == IntPtr.Zero)
          return -1;
        return position + Pointers.Offset32(start, result);
      };
      GetLineExtentFunction getLineExtent = position => {
        int lineStart;
        int lineLength;
        NativeMethods.UTF16_GetLineExtentFromPosition(Pointer, (int)CharacterCount, position, out lineStart, out lineLength);
        return new FilePositionSpan { Position = lineStart, Length = lineLength };
      };

      return new TextSourceTextSearch(getLineExtent, findEntry).FilterOnOtherEntries(searchContentsData.ParsedSearchString, matches);
    }
  }
}
