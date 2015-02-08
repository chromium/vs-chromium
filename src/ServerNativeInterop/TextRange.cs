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
    private readonly int _length;

    public TextRange(int position, int length) {
      if (position < 0 || length < 0)
        ThrowArgumentException();
      _position = position;
      _length = length;
    }

    public int Position {
      get { return _position; }
    }

    public int Length {
      get { return _length; }
    }

    public int EndPosition {
      get { return _position + _length; }
    }

    private static void ThrowArgumentException() {
      throw new ArgumentException();
    }
  }
}