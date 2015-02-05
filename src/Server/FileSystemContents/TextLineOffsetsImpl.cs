// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Win32.Strings;
using VsChromium.Server.NativeInterop;

namespace VsChromium.Server.FileSystemContents {
  public unsafe class TextLineOffsetsImpl {
    private const char LineBreak = '\n';
    private readonly FileContentsMemory _heap;  // Keep this ensure native memory lifetime
    private readonly int _characterSize;
    private readonly byte* _blockStart;
    private readonly byte* _blockEnd;
    private readonly List<int> _listStartOffsets = new List<int>();

    public TextLineOffsetsImpl(FileContentsMemory heap, int characterSize) {
      // Only ascii and unicode.
      if (characterSize != 1 && characterSize != 2) {
        throw new ArgumentException();
      }
      _heap = heap;
      _characterSize = characterSize;
      _blockStart = (byte *)heap.Pointer.ToPointer();
      _blockEnd = _blockStart + heap.ByteLength;
    }

    public void CollectLineOffsets() {
      if (_listStartOffsets.Any())
        throw new InvalidOperationException("Object already initialized.");

      AddOffset(0);
      for (byte* p = _blockStart; p < _blockEnd - _characterSize; p += _characterSize) {
        char ch;
        if (_characterSize == 1)
          ch = (char)*p;
        else
          ch = *(char*) p;
        if (ch == LineBreak) {
          var byteOffset = Pointers.Offset32(_blockStart, p) + _characterSize;
          AddOffset(byteOffset / _characterSize);
        }
      }
    }

    public FileExtract FilePositionSpanToFileExtract(FilePositionSpan filePositionSpan, int maxTextExtent) {
      maxTextExtent = Math.Max(maxTextExtent, filePositionSpan.Length);

      var spanStart = filePositionSpan.Position;
      var spanEnd = spanStart + filePositionSpan.Length;

      var lineStart = GetLineStart(spanStart, maxTextExtent);
      var lineEnd = GetLineEnd(spanEnd, maxTextExtent);

      // prefix - span - suffix
      var extractLength = lineEnd - lineStart;
      var spanLength = filePositionSpan.Length;
      Debug.Assert(spanLength <= extractLength);
      var prefixLength = Math.Min(spanStart - lineStart, maxTextExtent - spanLength);
      Debug.Assert(prefixLength >= 0);
      var suffixLength = Math.Min(lineEnd - spanEnd ,maxTextExtent - spanLength - prefixLength);
      Debug.Assert(suffixLength >= 0);

      lineStart = spanStart - prefixLength;
      lineEnd = spanEnd + suffixLength;
      var text = GetText(lineStart, lineEnd - lineStart);
      var lineCol = GetLineColumn(spanStart);

      return new FileExtract {
        Text = text,
        Offset = lineStart,
        Length = lineEnd - lineStart,
        LineNumber = lineCol.Item1,
        ColumnNumber = lineCol.Item2
      };
    }

    private Tuple<int, int> GetLineColumn(int offset) {
      var lineNumber = GetLineStartIndex(offset);
      Debug.Assert(lineNumber >= 0);
      Debug.Assert(lineNumber < _listStartOffsets.Count);

      var columnNumber = offset - _listStartOffsets[lineNumber];

      return Tuple.Create(lineNumber, columnNumber);
    }

    private int GetLineStartIndex(int offset) {
      Debug.Assert(offset >= 0);
      Debug.Assert(_listStartOffsets[0] == 0);

      var result = _listStartOffsets.BinarySearch(offset);
      if (result < 0) {
        var insertionIndex = ~result;
        return insertionIndex - 1;
      }
      return result;
    }

    private void AddOffset(int offset) {
      _listStartOffsets.Add(offset);
    }

    public FilePositionSpan GetLineExtent(int offset) {
      var lineNumber = GetLineStartIndex(offset);
      var lineStartOffset = _listStartOffsets[lineNumber];
      var lineEndOffset = (lineNumber == _listStartOffsets.Count - 1) ?
        Pointers.Offset32(_blockStart, _blockEnd) / _characterSize :
        _listStartOffsets[lineNumber + 1];
      return new FilePositionSpan {
        Position = lineStartOffset, 
        Length = lineEndOffset - lineStartOffset
      };
    }

    private int GetLineStart(int offset, int count) {
      var lineStartOffset = GetLineExtent(offset).Position;
      return Math.Max(lineStartOffset, offset - count);
    }

    private int GetLineEnd(int offset, int count) {
      var extent = GetLineExtent(offset);
      var lineEndOffset = extent.Position + extent.Length;
      return Math.Min(lineEndOffset, offset + count);
    }

    private string GetText(int offset, int length) {
      byte* start = Pointers.Add(_blockStart, offset * _characterSize);
      byte* end = Pointers.Add(_blockStart, (offset + length) * _characterSize);
      if (_characterSize == 1)
        return Conversion.Utf8ToString(start, end);
      else
        return Conversion.Utf16ToString(start, end);
    }
  }
}