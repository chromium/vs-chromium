// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class Utf16FileContents : FileContents {

    public Utf16FileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(heap, utcLastModified) {
    }

    public override int ByteLength { get { return _heap.ByteLength; } }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as Utf16FileContents;
      if (other2 == null)
        return false;

      return CompareBinaryContents(this, Pointer, ByteLength, other2, other2.Pointer, other2.ByteLength);
    }

    protected override ITextLineOffsets GetFileOffsets() {
      return new Utf16TextLineOffsets(_heap);
    }

    private IntPtr Pointer {
      get { return _heap.Pointer; }
    }

    protected override byte CharacterSize {
      get { return sizeof(char); }
    }

    protected override int CharacterCount {
      get {
        return _heap.ByteLength / CharacterSize;
      }
    }

    protected override TextFragment TextFragment {
      get {
        return new TextFragment(this.Pointer, 0, this.CharacterCount, this.CharacterSize);
      }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider) {
      return provider.GetUtf16Search();
    }

    protected override TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      int lineStart;
      int lineLength;
      NativeMethods.Utf16_GetLineExtentFromPosition(
          this.Pointer,
          this.CharacterCount,
          position,
          MaxLineExtentOffset,
          out lineStart,
          out lineLength);
      return new TextRange(lineStart, lineLength);
    }

    public static CompiledTextSearchBase CreateSearchAlgo(
        string pattern,
        SearchProviderOptions searchOptions) {
      var options = NativeMethods.SearchOptions.kNone;
      if (searchOptions.MatchCase) {
        options |= NativeMethods.SearchOptions.kMatchCase;
      }
      if (searchOptions.MatchWholeWord) {
        options |= NativeMethods.SearchOptions.kMatchWholeWord;
      }
      return new Utf16CompiledTextSearchStdSearch(pattern, options);
    }
  }
}
