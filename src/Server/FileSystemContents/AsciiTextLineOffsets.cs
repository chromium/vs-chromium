// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.Search;

namespace VsChromium.Server.FileSystemContents {
  public class AsciiTextLineOffsets : ITextLineOffsets {
    private readonly TextLineOffsetsImpl _impl;
    public AsciiTextLineOffsets(FileContentsMemory heap) {
      _impl = new TextLineOffsetsImpl(heap, sizeof(byte));
      _impl.CollectLineOffsets();
    }

    public FileExtract FilePositionSpanToFileExtract(FilePositionSpan filePositionSpan, int maxTextExtent) {
      return _impl.FilePositionSpanToFileExtract(filePositionSpan, maxTextExtent);
    }
  }
}
