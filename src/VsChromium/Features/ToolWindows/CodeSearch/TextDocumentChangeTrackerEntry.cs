// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class TextDocumentChangeTrackerEntry {
    private readonly FullPath _path;
    private readonly Dictionary<Tuple<int, int>, ITrackingSpan> _trackingSpans = new Dictionary<Tuple<int, int>, ITrackingSpan>();

    public TextDocumentChangeTrackerEntry(FullPath path) {
      _path = path;
    }

    public FilePositionsData PositionsData { get; set; }

    /// <summary>
    /// Called when a document is opened. We have to be careful that our
    /// internal spans may not be valid in the document buffer.
    /// </summary>
    public void CreateSpans(ITextBuffer buffer) {
      if (PositionsData == null)
        return;

      // Limit to 1,000 spans to avoid overloading the editor.
      var version = buffer.CurrentSnapshot.Version;
      foreach (var position in PositionsData.Positions.Take(1000)) {
        // Check range of span is ok (we don't know what happen to the files
        // on disk, so we have to be safe)
        if (0 <= position.Position && position.Position + position.Length <= version.Length) {
          var span = version.CreateTrackingSpan(new Span(position.Position, position.Length), SpanTrackingMode.EdgeExclusive);
          _trackingSpans.Add(Tuple.Create(position.Position, position.Length), span);
        }
      }
    }

    /// <summary>
    /// Call when a document is closed.
    /// </summary>
    public void DeleteSpans(ITextBuffer textBuffer) {
      _trackingSpans.Clear();
    }

    /// <summary>
    /// Translate a span value for this document into a "current snapshot" value.
    /// </summary>
    public Span TranslateSpan(Span value) {
      var key = Tuple.Create(value.Start, value.Length);
      var span = _trackingSpans.GetValue(key);
      if (span == null)
        return value;

      var newSpan = span.GetSpan(span.TextBuffer.CurrentSnapshot);
      return new Span(newSpan.Start, newSpan.Length);
    }
  }
}