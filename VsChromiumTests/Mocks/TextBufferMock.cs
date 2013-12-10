// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;

namespace VsChromiumTests.Mocks {
  class TextBufferMock : ITextBuffer {
    private readonly TextSnapshotMock _currentSnapshot;

    public TextBufferMock(string text) {
      _currentSnapshot = new TextSnapshotMock(this, text);
    }

    public PropertyCollection Properties { get; private set; }

    public ITextEdit CreateEdit(EditOptions options, int? reiteratedVersionNumber, object editTag) {
      throw new NotImplementedException();
    }

    public ITextEdit CreateEdit() {
      return new TextEditMock(this);
    }

    public IReadOnlyRegionEdit CreateReadOnlyRegionEdit() {
      throw new NotImplementedException();
    }

    public void TakeThreadOwnership() {
      throw new NotImplementedException();
    }

    public bool CheckEditAccess() {
      throw new NotImplementedException();
    }

    public void ChangeContentType(IContentType newContentType, object editTag) {
      throw new NotImplementedException();
    }

    public ITextSnapshot Insert(int position, string text) {
      throw new NotImplementedException();
    }

    public ITextSnapshot Delete(Span deleteSpan) {
      throw new NotImplementedException();
    }

    public ITextSnapshot Replace(Span replaceSpan, string replaceWith) {
      throw new NotImplementedException();
    }

    public bool IsReadOnly(int position) {
      throw new NotImplementedException();
    }

    public bool IsReadOnly(int position, bool isEdit) {
      throw new NotImplementedException();
    }

    public bool IsReadOnly(Span span) {
      throw new NotImplementedException();
    }

    public bool IsReadOnly(Span span, bool isEdit) {
      throw new NotImplementedException();
    }

    public NormalizedSpanCollection GetReadOnlyExtents(Span span) {
      throw new NotImplementedException();
    }

    public IContentType ContentType { get { throw new NotImplementedException(); } }
    public ITextSnapshot CurrentSnapshot { get { return _currentSnapshot; } }
    public bool EditInProgress { get { throw new NotImplementedException(); } }
    public event EventHandler<SnapshotSpanEventArgs> ReadOnlyRegionsChanged { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler<TextContentChangedEventArgs> Changed { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler<TextContentChangedEventArgs> ChangedLowPriority { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler<TextContentChangedEventArgs> ChangedHighPriority { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler<TextContentChangingEventArgs> Changing { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler PostChanged { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }

    public event EventHandler<ContentTypeChangedEventArgs> ContentTypeChanged { add { throw new NotImplementedException(); } remove { throw new NotImplementedException(); } }
  }
}
