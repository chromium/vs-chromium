// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Win32.Memory;
using VsChromiumCore.Win32.Strings;
using VsChromiumServer.VsChromiumNative;

namespace VsChromiumServer.Search {
  /// <summary>
  /// FileContents implementation for files containing only Ascii characters (e.g. all character
  /// values are less than 127).
  /// </summary>
  public class AsciiFileContents : FileContents {
    private const char _lineBreak = '\n';
    private const int _maxTextExtent = 50;
    private readonly SafeHeapBlockHandle _heap;
    private readonly int _textOffset;

    public AsciiFileContents(SafeHeapBlockHandle heap, int textOffset, DateTime utcLastWriteTime)
      : base(utcLastWriteTime) {
      if (textOffset > heap.ByteLength)
        throw new ArgumentException("Text offset is too far in buffer", "textOffset");
      _heap = heap;
      _textOffset = textOffset;
    }

    public override long ByteLength { get { return _heap.ByteLength - _textOffset; } }

    public IntPtr Pointer { get { return _heap.Pointer + _textOffset; } }

    public static AsciiStringSearchAlgorithm CreateSearchAlgo(string pattern, NativeMethods.SearchOptions searchOptions) {
      if (pattern.Length <= 64)
        return new AsciiStringSearchBndm64(pattern, searchOptions);
      else
        return new AsciiStringSearchBoyerMoore(pattern, searchOptions);
    }

    public override List<int> Search(SearchContentsData searchContentsData) {
      if (searchContentsData.Text.Length > ByteLength)
        return NoPositions;

      // TODO(rpaquay): We are limited to 2GB for now.
      return searchContentsData.AsciiStringSearchAlgo.SearchAll(Pointer, (int)ByteLength).ToList();
    }

    public override FileExtract SpanToLineExtract(FilePositionSpan filePositionSpan) {
      return SpanToLineExtractWorker(filePositionSpan);
    }

    public unsafe FileExtract SpanToLineExtractWorker(FilePositionSpan filePositionSpan) {
      var blockStart = Pointers.Add(_heap.Pointer, _textOffset);
      var blockEnd = Pointers.Add(_heap.Pointer, _heap.ByteLength);
      var textPosition = Pointers.Add(blockStart, filePositionSpan.Position);
      if (textPosition < blockStart || textPosition >= blockEnd)
        return null;

      var lineStart = GetLineStart(blockStart, blockEnd, textPosition, _maxTextExtent);
      Debug.Assert(blockStart <= lineStart);
      Debug.Assert(lineStart <= blockEnd);

      var lineEnd = GetLineEnd(blockStart, blockEnd, textPosition + filePositionSpan.Length, _maxTextExtent);
      Debug.Assert(blockStart <= lineEnd);
      Debug.Assert(lineEnd <= blockEnd);

      var text = Conversion.UTF8ToString(lineStart, lineEnd);

      var lineCol = GetLineNumber(blockStart, blockEnd, textPosition);

      return new FileExtract {
        Text = text,
        Offset = Pointers.Offset32(blockStart, lineStart),
        Length = Pointers.Offset32(lineStart, lineEnd),
        LineNumber = lineCol.Item1,
        ColumnNumber = lineCol.Item2
      };
    }

    private static unsafe Tuple<int, int> GetLineNumber(byte* blockStart, byte* blockEnd, byte* lineStart) {
      int lineNumber = 0;
      int columnNumber = 0;
      for (var p = blockStart; p < blockEnd; p++) {
        if (p == lineStart)
          break;
        if (*p == _lineBreak) {
          lineNumber++;
          columnNumber = 0;
        } else {
          columnNumber++;
        }
      }
      return Tuple.Create(lineNumber, columnNumber);
    }

    private static unsafe byte* GetLineStart(byte* start, byte* end, byte* position, int count) {
      var result = position;
      while (true) {
        if (count <= 0)
          break;

        var previous = result - 1;
        if (result == start)
          break;
        if (*previous == _lineBreak)
          break;

        count--;
        result = previous;
      }
      return result;
    }

    private static unsafe byte* GetLineEnd(byte* start, byte* end, byte* position, int count) {
      var result = position;
      while (true) {
        if (count <= 0)
          break;
        if (result == end)
          break;
        if (*result == _lineBreak)
          break;

        count--;
        result++;
      }
      return result;
    }
  }
}
