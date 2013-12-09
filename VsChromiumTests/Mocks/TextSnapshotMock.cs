using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumTests.Mocks {
  internal class TextSnapshotMock : ITextSnapshot {
    private readonly ITextBuffer _textBuffer;
    private readonly string _text;
    private readonly IList<ITextSnapshotLine> _lines;

    public TextSnapshotMock(TextBufferMock parent,  string text) {
      this._textBuffer = parent;
      this._text = text;
      this._lines = CreateLines(text).ToList();
    }

    private IEnumerable<ITextSnapshotLine> CreateLines(string text) {
      var lineNumber = 0;
      var index = 0;
      while(index < text.Length) {
        var lineEnding = text.IndexOf('\n', index);
        if (lineEnding < 0) {
          yield return new TextSnapshotLineMock(this, lineNumber, index, text.Length - index, "");
          yield break;
        }
        var lineEndingEnd = lineEnding + 1;
        if (lineEnding > index && text[lineEnding - 1] == '\r')
          lineEnding --;

        yield return new TextSnapshotLineMock(this, lineNumber, index, lineEnding - index, text.Substring(lineEnding, lineEndingEnd - lineEnding));

        index = lineEndingEnd;
        lineNumber++;
      }
    }

    public string GetText(Span span) {
      return this._text.Substring(span.Start, span.Length);
    }

    public string GetText(int startIndex, int length) {
      return GetText(new Span(startIndex, length));
    }

    public string GetText() {
      return GetText(0, this._text.Length);
    }

    public char[] ToCharArray(int startIndex, int length) {
      throw new NotImplementedException();
    }

    public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
      throw new NotImplementedException();
    }

    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode) {
      throw new NotImplementedException();
    }

    public ITrackingPoint CreateTrackingPoint(int position, PointTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
      throw new NotImplementedException();
    }

    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode) {
      throw new NotImplementedException();
    }

    public ITrackingSpan CreateTrackingSpan(Span span, SpanTrackingMode trackingMode, TrackingFidelityMode trackingFidelity) {
      throw new NotImplementedException();
    }

    public ITrackingSpan CreateTrackingSpan(int start, int length, SpanTrackingMode trackingMode) {
      throw new NotImplementedException();
    }

    public ITrackingSpan CreateTrackingSpan(
        int start,
        int length,
        SpanTrackingMode trackingMode,
        TrackingFidelityMode trackingFidelity) {
      throw new NotImplementedException();
    }

    public ITextSnapshotLine GetLineFromLineNumber(int lineNumber) {
      return this._lines[lineNumber];
    }

    public ITextSnapshotLine GetLineFromPosition(int position) {
      if (position == this.Length)
        return this._lines.Last();
      return this._lines.First(line => line.ExtentIncludingLineBreak.Contains(position));
    }

    public int GetLineNumberFromPosition(int position) {
      throw new NotImplementedException();
    }

    public void Write(TextWriter writer, Span span) {
      throw new NotImplementedException();
    }

    public void Write(TextWriter writer) {
      throw new NotImplementedException();
    }

    public ITextBuffer TextBuffer {
      get {
        return this._textBuffer;
      }
    }

    public IContentType ContentType {
      get {
        throw new NotImplementedException();
      }
    }

    public ITextVersion Version {
      get {
        throw new NotImplementedException();
      }
    }

    public int Length {
      get {
        return this._text.Length;
      }
    }

    public int LineCount {
      get {
        return this._lines.Count;
      }
    }

    public char this[int position] {
      get {
        return this._text[position];
      }
    }

    public IEnumerable<ITextSnapshotLine> Lines {
      get {
        return this._lines;
      }
    }
  }
}