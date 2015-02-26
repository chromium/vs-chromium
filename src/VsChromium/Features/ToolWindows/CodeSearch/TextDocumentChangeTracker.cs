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
  public class TextDocumentChangeTracker {
    private readonly IUIDelayedOperationProcessor _uiRequestProcessor;
    private readonly string requestId = typeof(TextDocumentChangeTracker).Name + Guid.NewGuid();
    private readonly Dictionary<FullPath, ITextDocument> _openDocuments = new Dictionary<FullPath, ITextDocument>();
    private readonly Dictionary<FullPath, TextDocumentChangeTrackerEntry> _trackingEntries = new Dictionary<FullPath, TextDocumentChangeTrackerEntry>();
    private readonly Dictionary<FullPath, FilePositionsData> _searchResults = new Dictionary<FullPath, FilePositionsData>();
    private bool _enabled;

    public TextDocumentChangeTracker(IUIDelayedOperationProcessor uiRequestProcessor) {
      _uiRequestProcessor = uiRequestProcessor;
    }

    public void Enable(DirectoryEntry searchResults) {
      _trackingEntries.Clear();

      _enabled = true;
      _searchResults.Clear();
      _uiRequestProcessor.Post(new DelayedOperation {
        Id = requestId,
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
              CreateEntryAndSpans(path, spans);
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

    /// <summary>
    /// Called when a TextDocument is open in the IDE.
    /// </summary>
    public void DocumentOpen(ITextDocument document, EventArgs args) {
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);
      _openDocuments[path] = document;

      CreateSpansForEntry(document, path);
    }

    public void DocumentClose(ITextDocument document, EventArgs args) {
      if (!FullPath.IsValid(document.FilePath))
        return;
      var path = new FullPath(document.FilePath);
      _openDocuments.Remove(path);

      DeleteSpansForEntry(document, path);
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

      if (args.FileActionType.HasFlag(FileActionTypes.ContentLoadedFromDisk)) {
        _trackingEntries.Remove(path);
      }

      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        _trackingEntries.Remove(path);
      }
    }

    private void DeleteSpansForEntry(ITextDocument document, FullPath path) {
      if (!_enabled)
        return;

      var entry = _trackingEntries.GetValue(path);
      if (entry == null)
        return;
      entry.DeleteSpans(document.TextBuffer);
    }

    private void CreateSpansForEntry(ITextDocument document, FullPath path) {
      if (!_enabled)
        return;

      var entry = _trackingEntries.GetValue(path);
      if (entry != null) {
        entry.CreateSpans(document.TextBuffer);
        return;
      }

      var spans = _searchResults.GetValue(path);
      if (spans != null) {
        CreateEntryAndSpans(path, spans);
        return;
      }
    }

    private void CreateEntryAndSpans(FullPath path, FilePositionsData spans) {
      var document = _openDocuments.GetValue(path);
      if (document != null) {
        var entry = new TextDocumentChangeTrackerEntry(path) {
          PositionsData = spans
        };
        entry.CreateSpans(document.TextBuffer);
        _trackingEntries[path] = entry;
      }
    }
  }
}