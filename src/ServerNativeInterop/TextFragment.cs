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
    private readonly long _characterOffset;
    private readonly long _characterCount;
    private readonly int _characterSize;

    public TextFragment(IntPtr textPtr, long characterOffset, long characterCount, int characterSize) {
      _textPtr = textPtr;
      _characterOffset = characterOffset;
      _characterCount = characterCount;
      _characterSize = characterSize;
    }

    public bool IsNull {
      get { return _textPtr == IntPtr.Zero; }
    }

    public bool IsEmpty {
      get { return _characterCount == 0; }
    }

    public IntPtr TextPtr {
      get { return _textPtr; }
    }

    public long CharacterOffset {
      get { return _characterOffset; }
    }

    public long CharacterCount {
      get { return _characterCount; }
    }

    public IntPtr FragmentStart {
      get {
        return Pointers.AddPtr(_textPtr, _characterOffset * _characterSize);
      }
    }

    public IntPtr FragmentEnd {
      get {
        return Pointers.AddPtr(FragmentStart, _characterCount * _characterSize);
      }
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="characterOffset"/> up
    /// to the end of this text fragment.
    /// </summary>
    public TextFragment Suffix(long characterOffset) {
      if (characterOffset < _characterOffset)
        throw new ArgumentException();

      var count = Math.Max(0, _characterCount - (characterOffset - _characterOffset));
      return new TextFragment(_textPtr, characterOffset, count, _characterSize);
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="characterStart"/>
    /// up to the end of this text fragment.
    /// </summary>
    public TextFragment Suffix(IntPtr characterStart) {
      return Suffix(Pointers.Offset64(_textPtr, characterStart));
    }

    /// <summary>
    /// Return a new fragment starting at <paramref name="characterStart"/>
    /// and containing <paramref name="characterCount"/> characters.
    /// </summary>
    public TextFragment Sub(IntPtr characterStart, long characterCount) {
      var byteOffset = Pointers.Offset64(_textPtr, characterStart);
      if (byteOffset < 0 || (byteOffset % _characterSize) != 0)
        throw new ArgumentException();
      return Sub(byteOffset / _characterSize, characterCount);
    }

    /// Return a new fragment starting at <paramref name="characterOffset"/>
    /// and containing <paramref name="characterCount"/> characters.
    public TextFragment Sub(long characterOffset, long characterCount) {
      if (characterOffset < 0 || characterCount < 0)
        throw new ArgumentException();
      return new TextFragment(_textPtr, characterOffset, characterCount, _characterSize);
    }
  }
}