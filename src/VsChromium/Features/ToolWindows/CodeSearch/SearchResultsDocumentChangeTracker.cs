// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Threads;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class SearchResultsDocumentChangeTracker {
    private readonly IDispatchThreadDelayedOperationExecutor _dispatchThreadRequestExecutor;
    private readonly ITextDocumentTable _textDocumentTable;
    private readonly string _requestId = typeof(SearchResultsDocumentChangeTracker).Name + Guid.NewGuid();
    private readonly Dictionary<FullPath, DocumentChangeTrackingEntry> _trackingEntries = new Dictionary<FullPath, DocumentChangeTrackingEntry>();
    private readonly Dictionary<FullPath, FilePositionsData> _searchResults = new Dictionary<FullPath, FilePositionsData>();
    private bool _enabled;

    public SearchResultsDocumentChangeTracker(
      IDispatchThreadDelayedOperationExecutor dispatchThreadRequestExecutor,
      ITextDocumentTable textDocumentTable) {
      _dispatchThreadRequestExecutor = dispatchThreadRequestExecutor;
      _textDocumentTable = textDocumentTable;
    }

    public void Enable(DirectoryEntry searchResults) {
      _trackingEntries.Clear();

      _enabled = true;
      _searchResults.Clear();

      // Delay to avoid processing results too often.
      _dispatchThreadRequestExecutor.Post(new DelayedOperation {
        Id = _requestId,
        Delay = TimeSpan.FromSeconds(1.0),
        Action = () => CreateEntries(searchResults)
      });
    }

    private void CreateEntries(DirectoryEntry searchResults) {
      if (!_enabled)
        return;

      using (new TimeElapsedLogger("Creating document tracking entries for search results", InfoLogger.Instance)) {
        _searchResults.Clear();
        foreach (DirectoryEntry projectRoot in searchResults.Entries) {
          var rootPath = new FullPath(projectRoot.Name);
          foreach (FileEntry fileEntry in projectRoot.Entries) {
            var path = rootPath.Combine(new RelativePath(fileEntry.Name));
            var spans = fileEntry.Data as FilePositionsData;
            if (spans != null) {
              _searchResults[path] = spans;

              // If the document is open, create the tracking spans now.
              var document = _textDocumentTable.GetOpenDocument(path);
              if (document != null) {
                var entry = new DocumentChangeTrackingEntry(spans);
                _trackingEntries[path] = entry;
                entry.CreateTrackingSpans(document.TextDocument.TextBuffer);
              }
            }
          }
        }
      }
    }

    public void Disable() {
      _enabled = false;
      _trackingEntries.Clear();
      _searchResults.Clear();
    }

    public Span? TranslateSpan(string filePath, Span? span) {
      if (!_enabled)
        return span;

      if (span == null)
        return null;

      var path = new FullPath(filePath);

      var entry = _trackingEntries.GetValue(path);
      if (entry == null)
        return span;

      return entry.TranslateSpan(span.Value);
    }

    public void DocumentOpen(ITextDocument document, EventArgs args) {
      if (!_enabled)
        return;
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);

      // Lookup or create the tracking entry for this document
      var entry = _trackingEntries.GetValue(path);
      if (entry == null) {
        // Create entry if it Is the path part of the search results
        var spans = _searchResults.GetValue(path);
        if (spans != null) {
          entry = new DocumentChangeTrackingEntry(spans);
          _trackingEntries[path] = entry;
        }
      }

      // Create the tracking spans.
      if (entry != null) {
        entry.CreateTrackingSpans(document.TextBuffer);
      }
    }

    public void DocumentClose(ITextDocument document, EventArgs args) {
      if (!_enabled)
        return;
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);

      // If search results enabled, deal with the tracking spans.
      var entry = _trackingEntries.GetValue(path);
      if (entry != null) {
        entry.DeleteTrackingSpans(document.TextBuffer);
      }
    }

    public void FileActionOccurred(ITextDocument document, TextDocumentFileActionEventArgs args) {
      if (!_enabled)
        return;
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);

      if (args.FileActionType.HasFlag(FileActionTypes.ContentSavedToDisk)) {
        var entry = _trackingEntries.GetValue(path);
        if (entry != null) {
          entry.UpdateSpansToLatestVersion(document.TextBuffer);
        }
      }

      if (args.FileActionType.HasFlag(FileActionTypes.ContentLoadedFromDisk)) {
        // TODO(rpaquay): Maybe the tracking spans are still valid?
        _trackingEntries.Remove(path);
      }

      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        // TODO(rpaquay): Support for redirecting to another file name?
        _trackingEntries.Remove(path);
      }
    }
  }
}