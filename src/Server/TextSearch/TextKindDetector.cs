// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Text;

namespace VsChromium.Server.TextSearch {
  public enum TextKind {
    Ascii,
    AsciiWithUtf8Bom,
    Utf8,
    Utf8WithBom,
    ProbablyBinary,
  }

  /// <summary>
  /// Detects the kind of contents of a file (ASCII text, UTF-8 text or binary).
  /// Managed port of <c>Text_GetKind</c> from the former VsChromium.Native.dll,
  /// with one fix: a file is reported as ASCII only if all its bytes are ASCII
  /// (the native heuristic only examines 50 slices of 4KB of large files).
  /// </summary>
  public static class TextKindDetector {
    private const int SliceCount = 50;
    private const int SliceSize = 4 * 1024;

    public static readonly int Utf8BomSize = 3;

    public static TextKind GetKind(ReadOnlySpan<byte> text) {
      var hasBom = HasUtf8Bom(text);
      var contents = hasBom ? text.Slice(Utf8BomSize) : text;
      var kind = GetContentKind(contents);
      // The heuristic below is based on samples of the contents: make sure we
      // never report "ASCII" for a file containing non ASCII bytes, as ASCII
      // files are searched as bytes, so that character positions would be
      // wrong otherwise (see "Behavior changes" in PORTING_PRINCIPLES.md).
      if (kind == ContentKind.Ascii && !Ascii.IsValid(contents)) {
        kind = ContentKind.Utf8;
      }
      switch (kind) {
        case ContentKind.Ascii:
          return hasBom ? TextKind.AsciiWithUtf8Bom : TextKind.Ascii;
        case ContentKind.Utf8:
          return hasBom ? TextKind.Utf8WithBom : TextKind.Utf8;
        default:
          return TextKind.ProbablyBinary;
      }
    }

    public static bool HasUtf8Bom(ReadOnlySpan<byte> text) {
      return text.Length >= 3 && text[0] == 0xEF && text[1] == 0xBB && text[2] == 0xBF;
    }

    private enum ContentKind {
      Ascii,
      Utf8,
      Binary,
    }

    private struct ContentCounts {
      // Simple (i.e. readable) ascii characters
      public int AsciiCount;
      // Extended UTF8 characters
      public int Utf8Count;
      // Others (i.e. not in previous 2 categories)
      public int OtherCount;
    }

    private static ContentKind GetContentKind(ReadOnlySpan<byte> text) {
      var counts = new ContentCounts();
      CountSlices(text, ref counts);

      var total = counts.AsciiCount + counts.Utf8Count + counts.OtherCount;
      if (total == 0) {
        return ContentKind.Ascii;
      }

      // Content is considered "binary" if more than 10% of characters are non
      // human readable.
      var otherRatio = (double)counts.OtherCount / total;
      if (otherRatio >= 0.1) {
        return ContentKind.Binary;
      }

      // Content is considered "Text", choose between pure ascii or mix of
      // ascii/utf8.
      return counts.Utf8Count == 0 ? ContentKind.Ascii : ContentKind.Utf8;
    }

    private static void CountSlices(ReadOnlySpan<byte> text, ref ContentCounts counts) {
      // Instead of going through the whole file contents (which can be big),
      // we limit ourselves to 50 slices of 4K each. This means we examine a
      // maximum of 200KB per file, whatever the size. For files smaller than
      // 200KB, we still examine the whole content.
      var betweenSlicesCount = (text.Length - SliceSize * SliceCount) / SliceCount;
      if (betweenSlicesCount <= 0) {
        CountFull(text, ref counts);
        return;
      }

      var offset = 0;
      while (offset < text.Length) {
        var sliceLength = Math.Min(SliceSize, text.Length - offset);
        CountFull(text.Slice(offset, sliceLength), ref counts);
        offset += sliceLength + betweenSlicesCount;
      }
    }

    private static void CountFull(ReadOnlySpan<byte> text, ref ContentCounts counts) {
      var index = 0;
      while (index < text.Length) {
        var ch = text[index];
        // See http://www.asciitable.com/
        if ((ch >= 32 && ch <= 126) || ch == '\t' || ch == '\r' || ch == '\n') {
          counts.AsciiCount++;
          index++;
        } else {
          var runeLength = GetUtf8RuneLength(text.Slice(index));
          if (runeLength > 0) {
            counts.Utf8Count++;
            index += runeLength;
          } else {
            counts.OtherCount++;
            index++;
          }
        }
      }
    }

    /// <summary>
    /// Returns the length of the UTF-8 sequence starting at the beginning of
    /// <paramref name="text"/>, or 0 if there is no valid sequence. Same
    /// (lenient) validation as the native implementation: only the lead and
    /// continuation bytes bit patterns are checked.
    /// </summary>
    private static int GetUtf8RuneLength(ReadOnlySpan<byte> text) {
      var ch = text[0];
      if ((ch & 0xF8) == 0xF0 && text.Length >= 4) {
        if (IsContinuation(text[1]) && IsContinuation(text[2]) && IsContinuation(text[3]))
          return 4;
      } else if ((ch & 0xF0) == 0xE0 && text.Length >= 3) {
        if (IsContinuation(text[1]) && IsContinuation(text[2]))
          return 3;
      } else if ((ch & 0xE0) == 0xC0 && text.Length >= 2) {
        if (IsContinuation(text[1]))
          return 2;
      }
      return 0;
    }

    private static bool IsContinuation(byte ch) {
      return (ch & 0xC0) == 0x80;
    }
  }
}
