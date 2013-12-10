// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.Text;

namespace VsChromiumTests.Mocks {
  class TextSnapshotLineMock : ITextSnapshotLine {
    private readonly ITextSnapshot _snapshot;
    private readonly int _lineNumber;
    private readonly int _index;
    private readonly int _length;
    private readonly string _lineSeparatorText;

    public TextSnapshotLineMock(TextSnapshotMock textSnapshotMock, int lineNumber, int index, int length, string lineSeparatorText) {
      _snapshot = textSnapshotMock;
      _lineNumber = lineNumber;
      _index = index;
      _length = length;
      _lineSeparatorText = lineSeparatorText;
    }

    public string GetText() {
      return _snapshot.GetText(_index, _length);
    }

    public string GetTextIncludingLineBreak() {
      return _snapshot.GetText(_index, _length + _lineSeparatorText.Length);
    }

    public string GetLineBreakText() {
      return _lineSeparatorText;
    }

    public ITextSnapshot Snapshot { get { return _snapshot; } }

    public SnapshotSpan Extent { get { return new SnapshotSpan(_snapshot, _index, _length); } }

    public SnapshotSpan ExtentIncludingLineBreak { get { return new SnapshotSpan(_snapshot, _index, _length + _lineSeparatorText.Length); } }

    public int LineNumber { get { return _lineNumber; } }

    public SnapshotPoint Start { get { return Extent.Start; } }

    public int Length { get { return _length; } }

    public int LengthIncludingLineBreak { get { return _length + _lineSeparatorText.Length; } }

    public SnapshotPoint End { get { return Extent.End; } }

    public SnapshotPoint EndIncludingLineBreak { get { return ExtentIncludingLineBreak.End; } }

    public int LineBreakLength { get { return _lineSeparatorText.Length; } }
  }
}
