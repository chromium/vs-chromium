// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.TextSearch {
  /// <summary>
  /// Abstraction over a fragment of text stored in native memory. Note: For
  /// performance reasons, the caller is responsible for ensuring the native
  /// memory is not freed as long as the fragment is in use.
  /// </summary>
  public struct TextFragment {
    private readonly IntPtr _textPtr;
    private readonly int _position;
    private readonly int _length;
    private readonly byte _characterSize;

    public TextFragment(IntPtr textPtr, int position, int length, byte characterSize) {
      if (position < 0 || length < 0 || characterSize < 1 || characterSize > 2)
        ThrowArgumentException();

      _textPtr = textPtr;
      _position = position;
      _length = length;
      _characterSize = characterSize;
    }

    /// <summary>
    /// The starting position of the text from the beginning of the native
    /// memory buffer.
    /// </summary>
    public int Position {
      get { return _position; }
    }

    /// <summary>
    /// The number of characters part of the fragment.
    /// </summary>
    public int Length {
      get { return _length; }
    }

    /// <summary>
    /// The size of a character in bytes: 1 for ASCII text, 2 for UTF-16 text.
    /// </summary>
    public byte CharacterSize {
      get { return _characterSize; }
    }

    /// <summary>
    /// The pointer corresponding to <see cref="Position"/>.
    /// </summary>
    public IntPtr StartPtr {
      get {
        return _textPtr + _position * _characterSize;
      }
    }

    /// Return a new fragment starting at <paramref name="index"/>
    /// and containing <paramref name="count"/> characters.
    public TextFragment Sub(int index, int count) {
      if (index < 0 || count < 0)
        ThrowArgumentException();
      return new TextFragment(_textPtr, index, count, _characterSize);
    }

    private static void ThrowArgumentException() {
      throw new ArgumentException();
    }
  }
}
