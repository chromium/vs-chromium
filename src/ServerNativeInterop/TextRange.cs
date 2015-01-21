// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Server.NativeInterop {
  /// <summary>
  /// Abstraction over a range of text, in terms of character offset and range
  /// length.
  /// </summary>
  public struct TextRange {
    private readonly long _characterOffset;
    private readonly long _characterCount;

    public TextRange(long characterOffset, long characterCount) {
      _characterOffset = characterOffset;
      _characterCount = characterCount;
    }

    public long CharacterOffset {
      get { return _characterOffset; }
    }

    public long CharacterCount {
      get { return _characterCount; }
    }

    public long CharacterEndOffset {
      get { return _characterOffset + _characterCount; }
    }
  }
}