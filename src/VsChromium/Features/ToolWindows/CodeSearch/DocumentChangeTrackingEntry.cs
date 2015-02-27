// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class DocumentChangeTrackingEntry {
    private readonly FilePositionsData _positionsData;
    private readonly Dictionary<Tuple<int, int>, ITrackingSpan> _trackingSpans = new Dictionary<Tuple<int, int>, ITrackingSpan>();

    public DocumentChangeTrackingEntry(FilePositionsData positionsData) {
      _positionsData = positionsData;
    }

    /// <summary>
    /// Called when a document is opened. We have to be careful that our
    /// internal spans may not be valid in the document buffer.
    /// </summary>
    public void CreateTrackingSpans(ITextBuffer buffer) {
      if (_positionsData == null)
        return;

      // Limit to 1,000 spans to avoid overloading the editor.
      var version = buffer.CurrentSnapshot.Version;
      foreach (var span in _positionsData.Positions.Take(1000)) {
        AddSpan(version, span);
      }
    }

    public void UpdateSpansToLatestVersion(ITextBuffer buffer) {
      // Copy and clear the current list.
      var currentSpans = _trackingSpans.Values.ToList();
      _trackingSpans.Clear();

      // Create new spans and update positions.
      var snapshot = buffer.CurrentSnapshot;
      foreach (var trackingSpan in currentSpans) {
        var currentSpan = trackingSpan.GetSpan(snapshot);
        var posData = new FilePositionSpan {
          Position = currentSpan.Start,
          Length = currentSpan.Length,
        };
        AddSpan(snapshot.Version, posData);
      }
    }

    private void AddSpan(ITextVersion version, FilePositionSpan position) {
      // Check range of span is ok (we don't know what happen to the files
      // on disk, so we have to be safe)
      if (0 <= position.Position && position.Position + position.Length <= version.Length) {
        // See http://blogs.msdn.com/b/noahric/archive/2009/06/06/editor-perf-markers-vs-tracking-spans.aspx
        // Never grows the tracking span.
        const SpanTrackingMode mode = SpanTrackingMode.EdgeExclusive;
        var span = version.CreateTrackingSpan(
          new Span(position.Position, position.Length),
          mode,
          TrackingFidelityMode.Forward);

        _trackingSpans.Add(Tuple.Create(position.Position, position.Length), span);
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