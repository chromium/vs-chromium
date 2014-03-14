using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Win32.Memory;
using VsChromiumCore.Win32.Strings;
using VsChromiumServer.NativeInterop;

namespace VsChromiumServer.Search {
  public unsafe class AsciiTextLineOffsets {
    private const char _lineBreak = '\n';
    private readonly SafeHeapBlockHandle _heap;  // Keep this ensure native memory lifetime
    private readonly byte* _blockStart;
    private readonly byte* _blockEnd;
    private readonly List<int> _listStartOffsets = new List<int>();

    public AsciiTextLineOffsets(SafeHeapBlockHandle heap, byte* blockStart, byte* blockEnd) {
      _heap = heap;
      _blockStart = blockStart;
      _blockEnd = blockEnd;
    }

    public void CollectLineOffsets() {
      if (_listStartOffsets.Any())
        throw new InvalidOperationException("Object already initialized.");

      AddOffset(0);
      for (var p = _blockStart; p < _blockEnd - 1; p++) {
        if (*p == _lineBreak) {
          AddOffset(Pointers.Offset32(_blockStart, p) + 1);
        }
      }
    }

    public FileExtract FilePositionSpanToFileExtract(FilePositionSpan filePositionSpan, int maxTextExtent) {
      var lineStart = GetLineStart(filePositionSpan.Position, maxTextExtent);
      var lineEnd = GetLineEnd(filePositionSpan.Position + filePositionSpan.Length, maxTextExtent);
      var text = GetText(lineStart, lineEnd - lineStart);
      var lineCol = GetLineColumn(filePositionSpan.Position);

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

    private int GetLineStart(int offset, int count) {
      var lineNumber = GetLineStartIndex(offset);
      var lineStartOffset = _listStartOffsets[lineNumber];
      return Math.Max(lineStartOffset, offset - count);
    }

    private int GetLineEnd(int offset, int count) {
      var lineNumber = GetLineStartIndex(offset);
      var lineEndOffset = (lineNumber == _listStartOffsets.Count - 1) ?
                            Pointers.Offset32(_blockStart, _blockEnd) :
                            _listStartOffsets[lineNumber + 1];
      return Math.Min(lineEndOffset, offset + count);
    }

    private string GetText(int offset, int length) {
      byte* start = Pointers.Add(_blockStart, offset);
      byte* end = Pointers.Add(_blockStart, offset + length);
      return Conversion.UTF8ToString(start, end);
    }
  }
}