// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
namespace VsChromium.Server.NativeInterop {
  /// <summary>
  /// Abstraction over a range of text, in terms of character offset and range
  /// length.
  /// </summary>
  public struct TextRange {
    private readonly int _position;
    private readonly int _count;

    public TextRange(int position, int count) {
      if (position < 0 || count < 0)
        ThrowArgumentException();
      _position = position;
      _count = count;
    }

    public int CharacterOffset {
      get { return _position; }
    }

    public int CharacterCount {
      get { return _count; }
    }

    public int CharacterEndOffset {
      get { return _position + _count; }
    }

    private static void ThrowArgumentException() {
      throw new ArgumentException();
    }
  }
}