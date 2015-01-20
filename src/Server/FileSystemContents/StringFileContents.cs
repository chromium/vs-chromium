// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;
using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class StringFileContents : FileContents {
    public static readonly StringFileContents Empty = new StringFileContents("");

    private readonly string _text;

    public StringFileContents(string text)
      : base(DateTime.MinValue) {
      _text = text;
    }

    public override int CharacterSize {
      get { return sizeof (char); }
    }

    public override long ByteLength { get { return _text.Length * 2; } }

    protected override long CharacterCount {
      get { return _text.Length; }
    }

    public override bool HasSameContents(FileContents other) {
      var other2 = other as StringFileContents;
      if (other2 == null)
        return false;
      return _text == other2._text;
    }

    protected override TextFragment TextFragment {
      get { return TextFragment.Null; }
    }

    protected override ICompiledTextSearch GetCompiledTextSearch(ICompiledTextSearchProvider provider) {
      throw new NotImplementedException();
    }

    protected override TextRange GetLineTextRangeFromPosition(long position, long maxRangeLength) {
      throw new NotImplementedException();
    }
  }
}
