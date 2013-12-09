using Microsoft.VisualStudio.Text;

namespace VsChromiumTests.Mocks {
  internal class TextSnapshotLineMock : ITextSnapshotLine {
    private readonly ITextSnapshot _snapshot;
    private readonly int _lineNumber;
    private readonly int _index;
    private readonly int _length;
    private readonly string _lineSeparatorText;

    public TextSnapshotLineMock(TextSnapshotMock textSnapshotMock, int lineNumber, int index, int length, string lineSeparatorText) {
      this._snapshot = textSnapshotMock;
      this._lineNumber = lineNumber;
      this._index = index;
      this._length = length;
      this._lineSeparatorText = lineSeparatorText;
    }

    public string GetText() {
      return this._snapshot.GetText(this._index, this._length);
    }

    public string GetTextIncludingLineBreak() {
      return this._snapshot.GetText(this._index, this._length + this._lineSeparatorText.Length);
    }

    public string GetLineBreakText() {
      return this._lineSeparatorText;
    }

    public ITextSnapshot Snapshot {
      get {
        return this._snapshot;
      }
    }

    public SnapshotSpan Extent {
      get {
        return new SnapshotSpan(this._snapshot, this._index, this._length);
      }
    }

    public SnapshotSpan ExtentIncludingLineBreak {
      get {
        return new SnapshotSpan(this._snapshot, this._index, this._length + this._lineSeparatorText.Length);
      }
    }

    public int LineNumber {
      get {
        return this._lineNumber;
      }
    }

    public SnapshotPoint Start {
      get {
        return this.Extent.Start;
      }
    }

    public int Length {
      get {
        return this._length;
      }
    }

    public int LengthIncludingLineBreak {
      get {
        return this._length + this._lineSeparatorText.Length;
      }
    }

    public SnapshotPoint End {
      get {
        return this.Extent.End;
      }
    }

    public SnapshotPoint EndIncludingLineBreak {
      get {
        return this.ExtentIncludingLineBreak.End;
      }
    }

    public int LineBreakLength {
      get {
        return this._lineSeparatorText.Length;
      }
    }
  }
}