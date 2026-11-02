// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.TextSearch;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    public AsciiFileContents(FileContentsMemory contents, DateTime utcLastModified)
      : base(contents, utcLastModified) {
    }

    protected override ITextLineOffsets GetFileOffsets() {
      return new AsciiTextLineOffsets(Contents);
    }

    public override byte CharacterSize {
      get { return sizeof(byte); }
    }

    protected override unsafe TrigramFilterSet CreateTrigramFilters(System.Collections.Generic.IReadOnlyList<TextRange> chunks) {
      return TrigramFilterSet.Build(new ReadOnlySpan<byte>(Contents.Pointer.ToPointer(), CharacterCount), chunks);
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchContainer container) {
      return container.GetAsciiSearch();
    }

    protected override unsafe TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      var text = new ReadOnlySpan<byte>(Contents.Pointer.ToPointer(), CharacterCount);
      return LineExtents.GetLineExtentFromPosition(text, position, MaxLineExtentOffset, (byte)'\n');
    }

    protected override unsafe int LastIndexOfNewLine(TextRange range) {
      var text = new ReadOnlySpan<byte>(Contents.Pointer.ToPointer(), CharacterCount).Slice(range.Position, range.Length);
      var index = text.LastIndexOf((byte)'\n');
      return index < 0 ? index : range.Position + index;
    }
  }
}
