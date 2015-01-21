// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class Utf16FileContents : FileContents {
    private readonly FileContentsMemory _heap;

    public Utf16FileContents(FileContentsMemory heap, DateTime utcLastModified)
      : base(utcLastModified) {
      _heap = heap;
    }

    public override int CharacterSize {
      get { return sizeof (char); }
    }

    public override long ByteLength { get { return _heap.ByteLength; } }

    private IntPtr Pointer { get { return _heap.Pointer; } }
    protected override long CharacterCount { get { return _heap.ByteLength / 2; } }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as Utf16FileContents;
      if (other2 == null)
        return false;
      return NativeMethods.Ascii_Compare(this.Pointer, this.ByteLength, other2.Pointer, other2.ByteLength);
    }

    protected override TextFragment TextFragment {
      get { return new TextFragment(this.Pointer, 0, this.CharacterCount, this.CharacterSize); }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider) {
      return provider.GetUtf16Search();
    }

    protected override TextRange GetLineTextRangeFromPosition(long position, long maxRangeLength) {
      int lineStart;
      int lineLength;
      NativeMethods.UTF16_GetLineExtentFromPosition(
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

    public static Utf16CompiledTextSearch CreateSearchAlgo(
        string pattern,
        SearchProviderOptions searchOptions) {
      var options = NativeMethods.SearchOptions.kNone;
      if (searchOptions.MatchCase) {
        options |= NativeMethods.SearchOptions.kMatchCase;
      }
      return new StrStrWCompiledTextSearch(pattern, options);
    }
  }
}
