// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;

namespace VsChromium.Views {
  /// <summary>
  /// Represents a fragment of an ITextSnapshotLine. Use the "Create" static method
  /// to create an instance.
  /// </summary>
  public struct TextLineFragment {
    [Flags]
    public enum Options {
      /// <summary>
      /// No special behavior.
      /// </summary>
      Default = 0,

      /// <summary>
      /// Return characters from |end| to |start| (i.e. reverse order) when enumerating characters
      /// </summary>
      Reverse = 0x0001,

      /// <summary>
      /// Line break characters are considered part of the text line.
      /// </summary>
      IncludeLineBreak = 0x0002,
    }

    /// <summary>
    /// The number of characters inside the text snapshot line.
    /// </summary>
    private readonly int _count;

    /// <summary>
    /// The Text snapshot line we wrap.
    /// </summary>
    private readonly ITextSnapshotLine _line;

    private readonly Options _options;

    /// <summary>
    /// The position inside the text snapshot line.
    /// </summary>
    private readonly int _position;

    private TextLineFragment(ITextSnapshotLine line, int position, int count, Options options) {
      if (count < 0)
        throw new ArgumentException("Negative count is not allowed", "count");

      if (position < line.Start.Position)
        throw new ArgumentException("Position can not be before line start", "position");

      if (position >= line.EndIncludingLineBreak.Position)
        throw new ArgumentException("Position can not be after line end", "position");

      _line = line;
      _position = position;
      _count = count;
      _options = options;
    }

    public ITextSnapshotLine Line { get { return _line; } }

    public Span Span { get { return new Span(_position, _count); } }

    public SnapshotSpan SnapshotSpan { get { return new SnapshotSpan(_line.Snapshot, Span); } }

    /// <summary>
    /// Return a fragment of a text line, safely ensuring that "start" and "end" position are within the boundaries
    /// of the line. |options| can be used to customize the behavior.
    /// </summary>
    public static TextLineFragment Create(ITextSnapshotLine line, int start, int end, TextLineFragment.Options options) {
      if (start < line.Start.Position) {
        start = line.Start.Position;
      }

      if ((options & TextLineFragment.Options.IncludeLineBreak) == TextLineFragment.Options.IncludeLineBreak) {
        if (end > line.EndIncludingLineBreak.Position)
          end = line.EndIncludingLineBreak.Position;
      } else {
        if (end > line.End.Position)
          end = line.End.Position;
      }

      if (start > end) {
        return new TextLineFragment(line, start, 0, options);
      }

      var count = end - start;
      return new TextLineFragment(line, start, count, options);
    }

    public IEnumerable<SnapshotPoint> GetPoints() {
      if ((_options & Options.Reverse) == Options.Reverse) {
        for (var i = _position + _count - 1; i >= _position; i--) {
          yield return new SnapshotPoint(_line.Snapshot, i);
        }
      } else {
        for (var i = _position; i < _position + _count; i++) {
          yield return new SnapshotPoint(_line.Snapshot, i);
        }
      }
    }

    public string GetText() {
      return GetText(0, _count);
    }

    public string GetText(int start, int count) {
      if (start < 0)
        start = 0;
      if (start >= _count)
        return string.Empty;
      if (count < 0)
        count = 0;
      if (count > _count - start)
        count = _count - start;
      return _line.Snapshot.GetText(_position + start, count);
    }
  }
}
