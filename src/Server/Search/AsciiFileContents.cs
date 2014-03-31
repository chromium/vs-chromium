// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
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
      return searchContentsData.AsciiStringSearchAlgo.SearchAll(Pointer, (int)ByteLength).ToList();
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
