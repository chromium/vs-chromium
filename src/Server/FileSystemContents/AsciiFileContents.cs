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
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    private const int MaxTextExtractLength = 50;
    private readonly FileContentsMemory _heap;

    public AsciiFileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(utcLastModified) {
      _heap = heap;
    }

    public override int CharacterSize {
      get { return sizeof (byte); }
    }

    public override long ByteLength { get { return _heap.ByteLength; } }

    private IntPtr Pointer { get { return _heap.Pointer; } }

    protected override long CharacterCount { get { return _heap.ByteLength; } }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as AsciiFileContents;
      if (other2 == null)
        return false;
      return NativeMethods.Ascii_Compare(this.Pointer, this.ByteLength, other2.Pointer, other2.ByteLength);
    }

    protected override TextFragment TextFragment {
      get { return new TextFragment(this.Pointer, 0, this.CharacterCount, this.CharacterSize); }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider) {
      return provider.GetAsciiSearch();
    }

    protected override TextRange GetLineTextRangeFromPosition(long position, long maxRangeLength) {
      int lineStart;
      int lineLength;
      NativeMethods.Ascii_GetLineExtentFromPosition(
        this.Pointer,
        // TODO(rpaquay): We are limited to 2GB for now.
        (int)this.CharacterCount,
        // TODO(rpaquay): We are limited to 2GB for now.
        (int)position,
        MaxLineExtentOffset,
        out lineStart,
        out lineLength);
      return new TextRange(lineStart, lineLength);
    }

    public static AsciiCompiledTextSearch CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (searchOptions.HasFlag(NativeMethods.SearchOptions.kRegex)) {
        //return new AsciiStringSearchRegex(pattern, searchOptions);
        return new AsciiCompiledTextSearchRe2(pattern, searchOptions);
      }

      if (pattern.Length <= 64)
        return new AsciiCompiledTextSearchBndm64(pattern, searchOptions);

      return new AsciiCompiledTextSearchBoyerMoore(pattern, searchOptions);
    }

    public override IEnumerable<FileExtract> GetFileExtracts(IEnumerable<FilePositionSpan> spans) {
      var offsets = new AsciiTextLineOffsets(_heap);
      offsets.CollectLineOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, MaxTextExtractLength))
        .Where(x => x != null)
        .ToList();
    }
  }
}
