// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.TextSearch {
  public static class LineExtents {
    /// <summary>
    /// Returns the extent of the line containing <paramref name="position"/>,
    /// looking at most <paramref name="maxOffset"/> characters before and
    /// after <paramref name="position"/>. The line extent includes the
    /// terminating '\n' character, if any. Managed port of
    /// <c>GetLineExtentFromPosition</c> from the former VsChromium.Native.dll.
    /// </summary>
    public static TextRange GetLineExtentFromPosition<TChar>(
      ReadOnlySpan<TChar> text,
      int position,
      int maxOffset,
      TChar newLine) where TChar : unmanaged, IEquatable<TChar> {
      if (position < 0 || position > text.Length)
        throw new ArgumentOutOfRangeException(nameof(position));

      var low = Math.Max(0, position - maxOffset);
      var high = (int)Math.Min(text.Length, (long)position + maxOffset);

      // Search backward up to "low" included
      var start = position;
      if (start > low) {
        var index = text.Slice(low, position - low).LastIndexOf(newLine);
        start = index < 0 ? low : low + index + 1;
      }

      // Search forward up to "high" excluded
      var end = high;
      if (position < high) {
        var index = text.Slice(position, high - position).IndexOf(newLine);
        if (index >= 0) {
          end = position + index + 1;
        }
      }

      return new TextRange(start, end - start);
    }
  }
}
