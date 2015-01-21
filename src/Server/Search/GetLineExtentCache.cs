// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Server.FileSystemContents;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.Search {
  public class GetLineExtentCache {
    private readonly GetLineRangeFunction _getLineRange;
    private TextRange? _previousSpan;

    public GetLineExtentCache(GetLineRangeFunction getLineRange) {
      _getLineRange = getLineRange;
    }

    public TextRange GetLineExtent(long position) {
      if (_previousSpan.HasValue) {
        if (position >= _previousSpan.Value.CharacterOffset &&
            position < _previousSpan.Value.CharacterOffset + _previousSpan.Value.CharacterCount) {
          return _previousSpan.Value;
        }
      }

      _previousSpan = _getLineRange(position);
      return _previousSpan.Value;
    }
  }
}