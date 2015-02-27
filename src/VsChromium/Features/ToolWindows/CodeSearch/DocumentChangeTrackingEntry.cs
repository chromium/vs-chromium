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
    private readonly Dictionary<Tuple<int, int>, Entry> _trackingSpans = new Dictionary<Tuple<int, int>, Entry>();

    private class Entry {
      public ITrackingSpan TrackingSpan { get; set; }
      public int Position { get; set; }
      public int Length { get; set; }
    }

    public DocumentChangeTrackingEntry(FilePositionsData positionsData) {
      _positionsData = positionsData;
    }

    /// <summary>
    /// Called when a document is opened. We have to be careful that our
    /// internal spans may not be valid in the document buffer.
    /// </summary>
    public void CreateTrackingSpans(ITextBuffer buffer) {
      var version = buffer.CurrentSnapshot.Version;
      if (_trackingSpans.Count > 0) {
        _trackingSpans.Values.ForAll(entry => {
          entry.TrackingSpan = CreateTrackingSpan(version, entry.Position, entry.Length);
        });
      } else {
        // Limit to 1,000 spans to avoid overloading the editor.
        foreach (var span in _positionsData.Positions.Take(1000)) {
          var entry = new Entry {
            Position = span.Position,
            Length = span.Length,
            TrackingSpan = CreateTrackingSpan(version, span.Position, span.Length)
          };
          _trackingSpans.Add(Tuple.Create(span.Position, span.Length), entry);
        }
      }
    }

    public void UpdateSpansToLatestVersion(ITextBuffer buffer) {
      var snapshot = buffer.CurrentSnapshot;
      _trackingSpans.Values.ForAll(entry => {
        if (entry.TrackingSpan == null)
          return;
        var currentSpan = entry.TrackingSpan.GetSpan(snapshot);
        entry.Position = currentSpan.Start;
        entry.Length = currentSpan.Length;
      });
    }

    /// <summary>
    /// May return <code>null</code> if span is outside range of document.
    /// </summary>
    private ITrackingSpan CreateTrackingSpan(ITextVersion version, int position, int length) {
      // Check range of span is ok (we don't know what happen to the files
      // on disk, so we have to be safe)
      if ((0 <= position) && (position + position <= version.Length)) {
        // See http://blogs.msdn.com/b/noahric/archive/2009/06/06/editor-perf-markers-vs-tracking-spans.aspx
        // Never grows the tracking span.
        const SpanTrackingMode mode = SpanTrackingMode.EdgeExclusive;
        var span = version.CreateTrackingSpan(
          new Span(position, length),
          mode,
          TrackingFidelityMode.Forward);
        return span;
      } else {
        return null;
      }
    }

    /// <summary>
    /// Call when a document is closed.
    /// </summary>
    public void DeleteTrackingSpans(ITextBuffer textBuffer) {
      _trackingSpans.Values.ForAll(x => x.TrackingSpan = null);
    }

    /// <summary>
    /// Translate a span value for this document into a "current snapshot" value.
    /// </summary>
    public Span TranslateSpan(Span value) {
      var key = Tuple.Create(value.Start, value.Length);
      var span = _trackingSpans.GetValue(key);
      if (span == null || span.TrackingSpan == null)
        return value;

      var newSpan = span.TrackingSpan.GetSpan(span.TrackingSpan.TextBuffer.CurrentSnapshot);
      return new Span(newSpan.Start, newSpan.Length);
    }
  }
}