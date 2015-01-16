// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    private const int _maxTextExtent = 50;
    private readonly FileContentsMemory _heap;

    public AsciiFileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(utcLastModified) {
      _heap = heap;
    }

    public override long ByteLength { get { return _heap.ByteLength; } }
    private IntPtr Pointer { get { return _heap.Pointer; } }
    private long CharacterCount { get { return _heap.ByteLength; } }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as AsciiFileContents;
      if (other2 == null)
        return false;
      return NativeMethods.Ascii_Compare(this.Pointer, this.ByteLength, other2.Pointer, other2.ByteLength);
    }

    public static AsciiStringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (searchOptions.HasFlag(NativeMethods.SearchOptions.kRegex))
        return new AsciiStringSearchRegex(pattern, searchOptions);

      if (pattern.Length <= 64)
        return new AsciiStringSearchBndm64(pattern, searchOptions);

      return new AsciiStringSearchBoyerMoore(pattern, searchOptions);
    }

    public override List<FilePositionSpan> Search(
        FileName fileName,
        SearchContentsData searchContentsData,
        IOperationProgressTracker progressTracker) {
      if (searchContentsData.ParsedSearchString.MainEntry.Text.Length > ByteLength)
        return NoSpans;

      var algo = searchContentsData.GetSearchAlgorithms(searchContentsData.ParsedSearchString.MainEntry).AsciiStringSearchAlgo;
      // TODO(rpaquay): We are limited to 2GB for now.
      var result = algo.SearchAll(fileName.RelativePath.Value, Pointer, (int)ByteLength, progressTracker);
      if (searchContentsData.ParsedSearchString.EntriesBeforeMainEntry.Count == 0 &&
          searchContentsData.ParsedSearchString.EntriesAfterMainEntry.Count == 0) {
        return result.ToList();
      }

      return FilterOnOtherEntries(searchContentsData, result).ToList();
    }

    private unsafe IEnumerable<FilePositionSpan> FilterOnOtherEntries(SearchContentsData searchContentsData, IEnumerable<FilePositionSpan> matches) {
      FindEntryFunction findEntry = (position, length, entry) => {
        var algo = searchContentsData.GetSearchAlgorithms(entry).AsciiStringSearchAlgo;
        var start = Pointers.AddPtr(this.Pointer, position);
        // Note: From C# spec: If E is zero, then no allocation is made, and
        // the pointer returned is implementation-defined. 
        byte* searchBuffer = stackalloc byte[algo.SearchBufferSize];
        var searchParams = new NativeMethods.SearchParams {
          TextStart = start,
          TextLength = length,
          SearchBuffer = new IntPtr(searchBuffer),
        };
        algo.Search(ref searchParams);
        if (searchParams.MatchStart == IntPtr.Zero)
          return -1;
        return position + Pointers.Offset32(start, searchParams.MatchStart);
      };
      GetLineExtentFunction getLineExtent = position => {
        int lineStart;
        int lineLength;
        NativeMethods.Ascii_GetLineExtentFromPosition(this.Pointer, (int)this.CharacterCount, position, out lineStart, out lineLength);
        return new FilePositionSpan { Position = lineStart, Length = lineLength };
      };

      return new TextSourceTextSearch(getLineExtent, findEntry).FilterOnOtherEntries(searchContentsData.ParsedSearchString, matches);
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
