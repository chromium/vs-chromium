// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.NativeInterop {
  /// <summary>
  /// Abstraction over a fragment of text stored in native memory.
  /// Note: For performance reasons, the caller is responsible for ensuring
  /// the native memory is not freed as long as the fragment is in use.
  /// </summary>
  public struct TextFragment {
    public static TextFragment Null;
    private readonly IntPtr _textPtr;
    private readonly int _position;
    private readonly int _length;
    private readonly byte _characterSize;

    public TextFragment(IntPtr textPtr, int position, int length, byte characterSize) {
      _textPtr = textPtr;
      _position = position;
      _length = length;
      _characterSize = characterSize;
    }

    public bool IsNull {
      get { return _textPtr == IntPtr.Zero; }
    }

    public bool IsEmpty {
      get { return _length == 0; }
    }

    public IntPtr StartPtr {
      get {
        return Pointers.AddPtr(_textPtr, _position * _characterSize);
      }
    }

    public int Position {
      get { return _position; }
    }

    public int Length {
      get { return _length; }
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="characterOffset"/> up
    /// to the end of this text fragment.
    /// </summary>
    public TextFragment Suffix(int characterOffset) {
      if (characterOffset < _position)
        throw new ArgumentException();

      var count = Math.Max(0, _length - (characterOffset - _position));
      return new TextFragment(_textPtr, characterOffset, count, _characterSize);
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="characterStart"/>
    /// and containing <paramref name="characterCount"/> characters.
    /// </summary>
    public TextFragment Sub(IntPtr characterStart, int characterCount) {
      var byteOffset = Pointers.Offset32(_textPtr, characterStart);
      if (byteOffset < 0 || (byteOffset % _characterSize) != 0)
        throw new ArgumentException();
      return Sub(byteOffset / _characterSize, characterCount);
    }

    /// Return a new fragment starting at <paramref name="index"/>
    /// and containing <paramref name="count"/> characters.
    public TextFragment Sub(int index, int count) {
      if (index < 0 || count < 0)
        throw new ArgumentException();
      return new TextFragment(_textPtr, index, count, _characterSize);
    }
  }
}