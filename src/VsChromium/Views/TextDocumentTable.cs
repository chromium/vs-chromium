// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  [Export(typeof(ITextDocumentTable))]
  public class TextDocumentTable : ITextDocumentTable {
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;
    private readonly RunningDocumentTable _runningDocumentTable;
    private readonly Lazy<bool> _firstRun;
    private readonly object _openDocumentsLock = new object();
    private readonly Dictionary<FullPath, ITextDocument> _openDocuments = new Dictionary<FullPath, ITextDocument>();
    private uint _runningDocTableEventsCookie;

    [ImportingConstructor]
    public TextDocumentTable(
      [Import(typeof(SVsServiceProvider))] IServiceProvider serviceProvider,
      ITextDocumentFactoryService textDocumentFactoryService,
      IVsEditorAdaptersFactoryService vsEditorAdaptersFactoryService) {
      _textDocumentFactoryService = textDocumentFactoryService;
      _vsEditorAdaptersFactoryService = vsEditorAdaptersFactoryService;
      _firstRun = new Lazy<bool>(FetchRunningDocumentTable);
      _runningDocumentTable = new RunningDocumentTable(serviceProvider);

      var vsDocTable = serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable4;
      if (vsDocTable != null) {
        var runningDocTableEvents = new VsRunningDocTableEvents(vsDocTable, vsEditorAdaptersFactoryService);
        runningDocTableEvents.DocumentLoaded += RunningDocTableEventsOnDocumentLoaded;
        runningDocTableEvents.DocumentClosed += RunningDocTableEventsOnDocumentClosed;
        runningDocTableEvents.DocumentRenamed += RunningDocTableEventsOnDocumentRenamed;
        _runningDocTableEventsCookie = _runningDocumentTable.Advise(runningDocTableEvents);
      } else {
        Logger.LogWarn("Error getting {0} from Service Provider", typeof(IVsRunningDocumentTable4).FullName);
      }
    }


    public event EventHandler<TextDocumentEventArgs> TextDocumentOpened;
    public event EventHandler<TextDocumentEventArgs> TextDocumentClosed;
    public event EventHandler<TextDocumentRenamedEventArgs> TextDocumentRenamed;

    public void Dispose() {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    public ITextDocument GetOpenDocument(FullPath path) {
      lock (_openDocumentsLock) {
        var unused = _firstRun.Value;
        return _openDocuments.GetValue(path);
      }
    }

    public IList<ITextDocument> GetOpenDocuments() {
      lock (_openDocumentsLock) {
        return _openDocuments.Values.ToList();
      }
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        if (_runningDocTableEventsCookie != 0 && _runningDocumentTable != null) {
          _runningDocumentTable.Unadvise(_runningDocTableEventsCookie);
          _runningDocTableEventsCookie = 0;
        }
      }
    }

    private void RunningDocTableEventsOnDocumentLoaded(object sender, DocumentLoadedEventArgs e) {
      var document = TextDocumentFromTextBuffer(e.TextBuffer);
      if (document == null) {
        Logger.LogInfo("Ignoring 'document loaded' event because the is no ITextDocument available: \"{0}\"", e.Path);
        return;
      }

      // Add to table (before invoking handlers). Note that this handler can be called
      // for both initial loading, as well as any reloading from disk. We want to call handlers
      // only once.
      bool added = false;
      lock (_openDocumentsLock) {
        if (!_openDocuments.ContainsKey(e.Path)) {
          _openDocuments[e.Path] = document;
          added = true;
        }
      }
      if (added) {
        OnTextDocumentOpened(new TextDocumentEventArgs(e.Path, document));
      }
    }

    private void RunningDocTableEventsOnDocumentClosed(object sender, DocumentClosedEventArgs e) {
      // Find document from out table, ignore if not found
      var document = TryLookupDocument(e.Path);
      if (document == null) {
        Logger.LogInfo("A document we don't know about was closed: \"{0}\"", e.Path);
        return;
      }

      // Call handlers (before removing from table)
      OnTextDocumentClosed(new TextDocumentEventArgs(e.Path, document));

      // Remove from table
      lock (_openDocumentsLock) {
        _openDocuments.Remove(e.Path);
      }
    }

    private void RunningDocTableEventsOnDocumentRenamed(object sender, DocumentRenamedEventArgs e) {
      // Find document from out table, ignore if not found
      var document = TryLookupDocument(e.OldPath);
      if (document == null) {
        Logger.LogInfo("A document we don't know about was renamed: \"{0}\"", e.OldPath);
        return;
      }

      // Update table (before invoking handlers)
      lock (_openDocumentsLock) {
        _openDocuments[e.NewPath] = document;
        _openDocuments.Remove(e.OldPath);
      }

      OnTextDocumentRenamed(new TextDocumentRenamedEventArgs(document, e.OldPath, e.NewPath));
    }

    private ITextDocument GetTextDocument(RunningDocumentInfo info) {
      // Get vs buffer
      IVsTextBuffer docData;
      try {
        docData = info.DocData as IVsTextBuffer;
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error getting IVsTextBuffer for document {0}, skipping document", info.Moniker);
        return null;
      }
      if (docData == null) {
        return null;
      }

      // Get ITextDocument
      var textBuffer = _vsEditorAdaptersFactoryService.GetDocumentBuffer(docData);
      if (textBuffer == null) {
        return null;
      }

      return TextDocumentFromTextBuffer(textBuffer);
    }

    private ITextDocument TextDocumentFromTextBuffer(ITextBuffer textBuffer) {
      ITextDocument document;
      if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document)) {
        return null;
      }

      return document;
    }

    private ITextDocument TryLookupDocument(FullPath path) {
      ITextDocument document;
      lock (_openDocumentsLock) {
        _openDocuments.TryGetValue(path, out document);
      }
      return document;
    }

    private bool FetchRunningDocumentTable() {
      foreach (var info in _runningDocumentTable) {
        if (!FullPath.IsValid(info.Moniker)) {
          continue;
        }
        var path = new FullPath(info.Moniker);

        var document = GetTextDocument(info);
        if (document == null) {
          continue;
        }

        lock (_openDocumentsLock) {
          _openDocuments[path] = document;
        }
      }
      // Dummy value
      return true;
    }

    protected virtual void OnTextDocumentOpened(TextDocumentEventArgs e) {
      Logger.WrapActionInvocation(() => { TextDocumentOpened?.Invoke(this, e); });
    }

    protected virtual void OnTextDocumentClosed(TextDocumentEventArgs e) {
      Logger.WrapActionInvocation(() => { TextDocumentClosed?.Invoke(this, e); });
    }

    protected virtual void OnTextDocumentRenamed(TextDocumentRenamedEventArgs e) {
      Logger.WrapActionInvocation(() => { TextDocumentRenamed?.Invoke(this, e); });
    }
  }
}
