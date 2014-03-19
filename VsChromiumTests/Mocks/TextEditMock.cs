// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Text;

namespace VsChromium.Tests.Mocks {
  class TextEditMock : ITextEdit {
    private readonly TextBufferMock _textBufferMock;
    private readonly ITextSnapshot _snapshot;
    private string _currentText;

    public TextEditMock(TextBufferMock textBufferMock) {
      _textBufferMock = textBufferMock;
      _snapshot = textBufferMock.CurrentSnapshot;
      _currentText = _snapshot.GetText();
    }

    public void Dispose() {
    }

    public ITextSnapshot Apply() {
      return new TextSnapshotMock(_textBufferMock, _currentText);
    }

    public void Cancel() {
      throw new NotImplementedException();
    }

    public ITextSnapshot Snapshot { get { return _snapshot; } }

    public bool Canceled { get { throw new NotImplementedException(); } }

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
      return Replace(replaceSpan.Start, replaceSpan.Length, replaceWith);
    }

    public bool Replace(int startPosition, int charsToReplace, string replaceWith) {
      var startText = _currentText.Substring(0, startPosition);
      var midText = replaceWith;
      var endText = _currentText.Substring(startPosition + charsToReplace);
      _currentText = startText + midText + endText;
      return true;
    }

    public bool HasEffectiveChanges { get { throw new NotImplementedException(); } }

    public bool HasFailedChanges { get { throw new NotImplementedException(); } }
  }
}
