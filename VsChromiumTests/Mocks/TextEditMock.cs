using System;
using Microsoft.VisualStudio.Text;

namespace VsChromiumTests.Mocks {
  internal class TextEditMock : ITextEdit {
    private readonly TextBufferMock _textBufferMock;
    private readonly ITextSnapshot _snapshot;
    private string _currentText;

    public TextEditMock(TextBufferMock textBufferMock) {
      this._textBufferMock = textBufferMock;
      this._snapshot = textBufferMock.CurrentSnapshot;
      this._currentText = this._snapshot.GetText();
    }

    public void Dispose() {
    }

    public ITextSnapshot Apply() {
      return new TextSnapshotMock(this._textBufferMock, this._currentText);
    }

    public void Cancel() {
      throw new NotImplementedException();
    }

    public ITextSnapshot Snapshot {
      get {
        return this._snapshot;
      }
    }

    public bool Canceled {
      get {
        throw new NotImplementedException();
      }
    }

    public bool Insert(int position, string text) {
      throw new NotImplementedException();
    }

    public bool Insert(int position, char[] characterBuffer, int startIndex, int length) {
      throw new NotImplementedException();
    }

    public bool Delete(Span deleteSpan) {
      throw new NotImplementedException();
    }

    public bool Delete(int startPosition, int charsToDelete) {
      throw new NotImplementedException();
    }

    public bool Replace(Span replaceSpan, string replaceWith) {
      return this.Replace(replaceSpan.Start, replaceSpan.Length, replaceWith);
    }

    public bool Replace(int startPosition, int charsToReplace, string replaceWith) {
      var startText = this._currentText.Substring(0, startPosition);
      var midText = replaceWith;
      var endText = this._currentText.Substring(startPosition + charsToReplace);
      this._currentText = startText + midText + endText;
      return true;
    }

    public bool HasEffectiveChanges {
      get {
        throw new NotImplementedException();
      }
    }

    public bool HasFailedChanges {
      get {
        throw new NotImplementedException();
      }
    }
  }
}