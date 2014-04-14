// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.Search {
  public class GetLineExtentCache {
    private readonly GetLineExtentFunction _getLineExtent;
    private FilePositionSpan? _previousSpan;

    public GetLineExtentCache(GetLineExtentFunction getLineExtent) {
      _getLineExtent = getLineExtent;
    }

    public FilePositionSpan GetLineExtent(int position) {
      if (_previousSpan.HasValue) {
        if (position >= _previousSpan.Value.Position &&
            position < _previousSpan.Value.Position + _previousSpan.Value.Length) {
          return _previousSpan.Value;
        }
      }

      _previousSpan = _getLineExtent(position);
      return _previousSpan.Value;
    }
  }
}