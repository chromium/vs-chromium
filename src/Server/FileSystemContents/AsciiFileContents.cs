// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Win32.Memory;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    public AsciiFileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(heap, utcLastModified) {
    }

    public static AsciiFileContents Empty = CreateEmpty();

    private static AsciiFileContents CreateEmpty() {
      var block = HeapAllocStatic.Alloc(1);
      Marshal.WriteByte(block.Pointer, 0);
      var mem = new FileContentsMemory(block, 0, 0);
      return new AsciiFileContents(mem, DateTime.MinValue);
    }

    public override long ByteLength { get { return _heap.ByteLength; } }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as AsciiFileContents;
      if (other2 == null)
        return false;

      return CompareBinaryContents(this, Pointer, ByteLength, other2, other2.Pointer, other2.ByteLength);
    }

    public override IEnumerable<FileExtract> GetFileExtracts(int maxLength, IEnumerable<FilePositionSpan> spans) {
      var offsets = new AsciiTextLineOffsets(_heap);
      offsets.CollectLineOffsets();

      return spans
        .Select(x => offsets.FilePositionSpanToFileExtract(x, maxLength))
        .Where(x => x != null)
        .ToList();
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

    private IntPtr Pointer { get { return _heap.Pointer; } }

    protected override int CharacterSize {
      get { return sizeof(byte); }
    }

    protected override long CharacterCount { get { return _heap.ByteLength; } }

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
  }
}
