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
    /// <summary>UTF-16 little endian text, with a byte order mark (FF FE).</summary>
    Utf16LittleEndianWithBom,
    /// <summary>UTF-16 big endian text, with a byte order mark (FE FF).</summary>
    Utf16BigEndianWithBom,
    Binary,
  }

  /// <summary>
  /// Detects the kind of contents of a file (text and its encoding, or binary), with the rules of
  /// the well known tools (git, ripgrep, VS Code):
  /// <list type="number">
  /// <item>A byte order mark identifies the encoding of a text file: UTF-8 (EF BB BF), UTF-16
  /// little endian (FF FE) or big endian (FE FF).</item>
  /// <item>Otherwise, a NUL byte in the first <see cref="BinaryDetectionPrefixLength"/> bytes means
  /// the file is binary (git's <c>buffer_is_binary</c>, same prefix length).</item>
  /// <item>Otherwise the file is text: ASCII if all its bytes are ASCII, else UTF-8 (invalid
  /// sequences are decoded as U+FFFD, so that positions are still correct).</item>
  /// </list>
  /// The decision only depends on the first <see cref="BinaryDetectionPrefixLength"/> bytes
  /// (<see cref="IsBinaryPrefix"/>), so that binary files can be skipped without reading them.
  /// See "Behavior changes" in docs/portable-server/PORTING_PRINCIPLES.md (this replaced the
  /// heuristic of the native implementation: 10% of non text bytes in 50 samples of 4 KB).
  /// </summary>
  public static class TextKindDetector {
    /// <summary>
    /// Number of bytes examined to detect binary files (same as git, "FIRST_FEW_BYTES").
    /// </summary>
    public const int BinaryDetectionPrefixLength = 8000;

    public static readonly int Utf8BomSize = 3;
    public static readonly int Utf16BomSize = 2;

    public static TextKind GetKind(ReadOnlySpan<byte> text) {
      if (HasUtf8Bom(text)) {
        return Ascii.IsValid(text.Slice(Utf8BomSize)) ? TextKind.AsciiWithUtf8Bom : TextKind.Utf8WithBom;
      }
      var utf16 = GetUtf16Bom(text);
      if (utf16 != null) {
        return utf16.Value;
      }
      if (ContainsNulInPrefix(text)) {
        return TextKind.Binary;
      }
      return Ascii.IsValid(text) ? TextKind.Ascii : TextKind.Utf8;
    }

    /// <summary>
    /// Returns true if a file starting with <paramref name="prefix"/> (its first
    /// <see cref="BinaryDetectionPrefixLength"/> bytes, or the whole file if shorter) is binary,
    /// i.e. if <see cref="GetKind"/> returns <see cref="TextKind.Binary"/> for the whole file.
    /// </summary>
    public static bool IsBinaryPrefix(ReadOnlySpan<byte> prefix) {
      return !HasUtf8Bom(prefix) && GetUtf16Bom(prefix) == null && ContainsNulInPrefix(prefix);
    }

    public static bool HasUtf8Bom(ReadOnlySpan<byte> text) {
      return text.Length >= 3 && text[0] == 0xEF && text[1] == 0xBB && text[2] == 0xBF;
    }

    public static bool HasUtf16Bom(ReadOnlySpan<byte> text) {
      return GetUtf16Bom(text) != null;
    }

    private static TextKind? GetUtf16Bom(ReadOnlySpan<byte> text) {
      if (text.Length >= 2) {
        if (text[0] == 0xFF && text[1] == 0xFE) {
          // Note: FF FE 00 00 is the UTF-32 little endian BOM: not supported (binary if NUL bytes).
          if (text.Length >= 4 && text[2] == 0 && text[3] == 0)
            return null;
          return TextKind.Utf16LittleEndianWithBom;
        }
        if (text[0] == 0xFE && text[1] == 0xFF) {
          return TextKind.Utf16BigEndianWithBom;
        }
      }
      return null;
    }

    private static bool ContainsNulInPrefix(ReadOnlySpan<byte> text) {
      return text.Slice(0, Math.Min(text.Length, BinaryDetectionPrefixLength)).IndexOf((byte)0) >= 0;
    }
  }
}
