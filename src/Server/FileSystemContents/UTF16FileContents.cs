// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.TextSearch;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class Utf16FileContents : FileContents {

    public Utf16FileContents(FileContentsMemory contents, DateTime utcLastModified)
      : base(contents, utcLastModified) {
    }

    protected override ITextLineOffsets GetFileOffsets() {
      return new Utf16TextLineOffsets(Contents);
    }

    public override byte CharacterSize {
      get { return sizeof(char); }
    }

    protected override unsafe TrigramFilterSet CreateTrigramFilters(System.Collections.Generic.IReadOnlyList<TextRange> chunks) {
      return TrigramFilterSet.Build(new ReadOnlySpan<char>(Contents.Pointer.ToPointer(), CharacterCount), chunks);
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container) {
      return container.GetUtf16Search();
    }

    protected override unsafe TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      var text = new ReadOnlySpan<char>(Contents.Pointer.ToPointer(), CharacterCount);
      return LineExtents.GetLineExtentFromPosition(text, position, MaxLineExtentOffset, '\n');
    }

    protected override unsafe int LastIndexOfNewLine(TextRange range) {
      var text = new ReadOnlySpan<char>(Contents.Pointer.ToPointer(), CharacterCount).Slice(range.Position, range.Length);
      var index = text.LastIndexOf('\n');
      return index < 0 ? index : range.Position + index;
    }
  }
}
