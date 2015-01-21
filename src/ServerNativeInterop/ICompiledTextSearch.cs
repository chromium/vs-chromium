// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Server.NativeInterop {
  /// <summary>
  /// Abstraction over text search algorithm pre-compiled with the search pattern.
  /// </summary>
  public interface ICompiledTextSearch : IDisposable {
    /// <summary>
    /// Find all occurrences of the search pattern in the given text fragment.
    /// </summary>
    IEnumerable<FilePositionSpan> SearchAll(TextFragment textFragment, IOperationProgressTracker progressTracker);

    /// <summary>
    /// Find the first occurrence of the search pattern in the given text fragment.
    /// </summary>
    FilePositionSpan? SearchOne(TextFragment textFragment, IOperationProgressTracker progressTracker);
  }

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

    public TextFragment Suffix(long characterOffset) {
      if (characterOffset < _characterOffset)
        throw new ArgumentException();

      var count = Math.Max(0, _characterCount - (characterOffset - _characterOffset));
      return new TextFragment(_textPtr, characterOffset, count, _characterSize);
    }

    public TextFragment Suffix(IntPtr fragmentStart) {
      return Suffix(Pointers.Offset64(_textPtr, fragmentStart));
    }

    public TextFragment Sub(IntPtr characterPtr, long characterCount) {
      var byteOffset = Pointers.Offset64(_textPtr, characterPtr);
      if (byteOffset < 0 || (byteOffset % _characterSize) != 0)
        throw new ArgumentException();
      return Sub(byteOffset / _characterSize, characterCount);
    }

    public TextFragment Sub(long characterOffset, long characterCount) {
      if (characterOffset < 0 || characterCount < 0)
        throw new ArgumentException();
      return new TextFragment(_textPtr, characterOffset, characterCount, _characterSize);
    }
  }
}