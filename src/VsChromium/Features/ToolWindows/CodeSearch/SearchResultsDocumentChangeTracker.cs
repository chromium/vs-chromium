// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Threads;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class SearchResultsDocumentChangeTracker {
    private readonly IUIDelayedOperationProcessor _uiRequestProcessor;
    private readonly string _requestId = typeof(SearchResultsDocumentChangeTracker).Name + Guid.NewGuid();
    private readonly Dictionary<FullPath, ITextDocument> _openDocuments = new Dictionary<FullPath, ITextDocument>();
    private readonly Dictionary<FullPath, DocumentChangeTrackingEntry> _trackingEntries = new Dictionary<FullPath, DocumentChangeTrackingEntry>();
    private readonly Dictionary<FullPath, FilePositionsData> _searchResults = new Dictionary<FullPath, FilePositionsData>();
    private bool _enabled;

    public SearchResultsDocumentChangeTracker(IUIDelayedOperationProcessor uiRequestProcessor) {
      _uiRequestProcessor = uiRequestProcessor;
    }

    public void Enable(DirectoryEntry searchResults) {
      _trackingEntries.Clear();

      _enabled = true;
      _searchResults.Clear();

      // Delay to avoid processing results too often.
      _uiRequestProcessor.Post(new DelayedOperation {
        Id = _requestId,
        Delay = TimeSpan.FromSeconds(1.0),
        Action = () => CreateEntries(searchResults)
      });
    }

    private void CreateEntries(DirectoryEntry searchResults) {
      if (!_enabled)
        return;

      using (new TimeElapsedLogger("Creating document tracking entries for search results")) {
        _searchResults.Clear();
        foreach (DirectoryEntry projectRoot in searchResults.Entries) {
          var rootPath = new FullPath(projectRoot.Name);
          foreach (FileEntry fileEntry in projectRoot.Entries) {
            var path = rootPath.Combine(new RelativePath(fileEntry.Name));
            var spans = fileEntry.Data as FilePositionsData;
            if (spans != null) {
              _searchResults[path] = spans;

              // If the document is open, create the tracking spans now.
              var document = _openDocuments.GetValue(path);
              if (document != null) {
                var entry = new DocumentChangeTrackingEntry(spans);
                _trackingEntries[path] = entry;
                entry.CreateTrackingSpans(document.TextBuffer);
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
      // Keep track of opened document in any case.
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);
      _openDocuments[path] = document;

      // If search results enabled, deal with the tracking spans.
      OnDocumentOpen(document, path);
    }

    public void DocumentClose(ITextDocument document, EventArgs args) {
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);
      _openDocuments.Remove(path);

      // If search results enabled, deal with the tracking spans.
      OnDocumentClose(document, path);
    }

    public void FileActionOccurred(ITextDocument document, TextDocumentFileActionEventArgs args) {
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);

      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        _openDocuments[new FullPath(args.FilePath)] = document;
        _openDocuments.Remove(path);
      }

      if (!_enabled)
        return;

      if (args.FileActionType.HasFlag(FileActionTypes.ContentSavedToDisk)) {
        OnDocumentSaved(document, path);
      }

      if (args.FileActionType.HasFlag(FileActionTypes.ContentLoadedFromDisk)) {
        _trackingEntries.Remove(path);
      }

      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        _trackingEntries.Remove(path);
      }
    }

    /// <summary>
    /// Create the tracking spsn when document opens.
    /// </summary>
    private void OnDocumentOpen(ITextDocument document, FullPath path) {
      if (!_enabled)
        return;

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

    /// <summary>
    /// Update the tracking spans to the lastesr version on document saved (so
    /// that the spans are valid when the document is open again).
    /// </summary>
    private void OnDocumentSaved(ITextDocument document, FullPath path) {
      var entry = _trackingEntries.GetValue(path);
      if (entry != null) {
        entry.UpdateSpansToLatestVersion(document.TextBuffer);
      }
    }

    /// <summary>
    /// Remove the tracking spans when the document is closed.
    /// </summary>
    private void OnDocumentClose(ITextDocument document, FullPath path) {
      if (!_enabled)
        return;

      var entry = _trackingEntries.GetValue(path);
      if (entry != null) {
        entry.DeleteSpans(document.TextBuffer);
      }
    }
  }
}