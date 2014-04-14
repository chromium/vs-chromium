// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Server.NativeInterop {
  public abstract class AsciiStringSearchAlgorithm : IDisposable {
    public abstract int PatternLength { get; }

    public virtual void Dispose() {
    }

    /// <summary>
    /// Find all occurrences of the pattern in the text block starting at
    /// |<paramref name="textPtr"/>| and containing |<paramref name="textLen"/>|
    /// characters.
    /// </summary>
    public IEnumerable<FilePositionSpan> SearchAll(IntPtr textPtr, int textLen) {
      var currentPtr = textPtr;
      var remainingLength = textLen;
      while (true) {
        currentPtr = Search(currentPtr, remainingLength);
        if (currentPtr == IntPtr.Zero)
          break;

        // TODO(rpaquay): We are limited to 2GB for now.
        var offset = Pointers.Offset32(textPtr, currentPtr);
        yield return new FilePositionSpan {Position = offset, Length = PatternLength};
        currentPtr += PatternLength;
        remainingLength = textLen - offset - PatternLength;
      }
    }

    /// <summary>
    /// Find the first occurrence of the pattern in the text block starting at
    /// |<paramref name="textPtr"/>| and containing |<paramref name="textLen"/>|
    /// characters. Returns |IntPtr.Zero| if the pattern is not present.
    /// </summary>
    public abstract IntPtr Search(IntPtr textPtr, int textLen);
  }
}
