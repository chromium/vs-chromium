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
      var result = new List<FilePositionSpan>();
      NativeMethods.SearchCallback matchFound = (matchStart, matchLength) => {
        // TODO(rpaquay): We are limited to 2GB for now.
        var offset = Pointers.Offset32(textPtr, matchStart);
        result.Add(new FilePositionSpan {
          Position = offset,
          Length = matchLength
        });
        return true; // We have no cancellation mechanism right now.
      };
      this.Search(textPtr, textLen, matchFound);
      return result;
    }

    /// <summary>
    /// Find all occurrences of the pattern in the text block starting at
    /// |<paramref name="textPtr"/>| and containing |<paramref name="textLen"/>|
    /// characters.
    /// </summary>
    public abstract void Search(IntPtr textPtr, int textLen, NativeMethods.SearchCallback matchFound);
  }
}
