// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;
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

    public override bool HasSameContents(FileContents other) {
      var other2 = other as AsciiFileContents;
      if (other2 == null)
        return false;

      return CompareBinaryContents(
        this, Contents.Pointer, ByteLength,
        other2, other2.Contents.Pointer, other2.ByteLength);
    }

    protected override ITextLineOffsets GetFileOffsets() {
      return new AsciiTextLineOffsets(Contents);
    }

    public static ICompiledTextSearch CreateSearchAlgo(string pattern, SearchProviderOptions searchOptions) {
      var options = NativeMethods.SearchOptions.kNone;
      if (searchOptions.MatchCase) {
        options |= NativeMethods.SearchOptions.kMatchCase;
      }
      if (searchOptions.MatchWholeWord) {
        options |= NativeMethods.SearchOptions.kMatchWholeWord;
      }

      if (searchOptions.UseRegex && searchOptions.UseRe2Engine)
        return new AsciiCompiledTextSearchRe2(pattern, options);

      if (searchOptions.UseRegex)
        return new AsciiCompiledTextSearchRegex(pattern, options);

      if (pattern.Length <= 64)
        return new AsciiCompiledTextSearchBndm64(pattern, options);

      return new AsciiCompiledTextSearchBoyerMoore(pattern, options);
    }

    protected override byte CharacterSize {
      get { return sizeof(byte); }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider) {
      return provider.GetAsciiSearch();
    }

    protected override TextRange GetLineTextRangeFromPosition(int position, int maxRangeLength) {
      int lineStart;
      int lineLength;
      NativeMethods.Ascii_GetLineExtentFromPosition(
        this.Contents.Pointer,
        this.CharacterCount,
        position,
        MaxLineExtentOffset,
        out lineStart,
        out lineLength);
      return new TextRange(lineStart, lineLength);
    }
  }
}
