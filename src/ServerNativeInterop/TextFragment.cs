// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.NativeInterop {
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
    /// The pointer corresponding to <see cref="Position"/>.
    /// </summary>
    public IntPtr StartPtr {
      get {
        return Pointers.AddPtr(_textPtr, _position * _characterSize);
      }
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="index"/> up to the end
    /// of this text fragment. <paramref name="index"/> must be comprised
    /// between <see cref="Position"/> and <see cref="Position"/> + <see
    /// cref="Length"/>.
    /// </summary>
    public TextFragment Sub(int index) {
      if (index < _position || index > _position + _length)
        ThrowArgumentException();

      var length = _position + _length - index;
      return new TextFragment(_textPtr, index, length, _characterSize);
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="startPtr"/>
    /// and containing <paramref name="count"/> characters.
    /// </summary>
    public TextFragment Sub(IntPtr startPtr, int count) {
      var byteOffset = Pointers.Offset32(_textPtr, startPtr);
      if (byteOffset < 0 || (byteOffset % _characterSize) != 0)
        ThrowArgumentException();
      return Sub(byteOffset / _characterSize, count);
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