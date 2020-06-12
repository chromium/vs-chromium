// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
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
    private readonly IFileSystem _fileSystem;
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
      IFileSystem fileSystem,
      ITextDocumentFactoryService textDocumentFactoryService,
      IVsEditorAdaptersFactoryService vsEditorAdaptersFactoryService) {
      _fileSystem = fileSystem;
      _textDocumentFactoryService = textDocumentFactoryService;
      _vsEditorAdaptersFactoryService = vsEditorAdaptersFactoryService;
      _firstRun = new Lazy<bool>(FetchRunningDocumentTable);
      _runningDocumentTable = new RunningDocumentTable(serviceProvider);

      var vsDocTable = serviceProvider.GetService(typeof(SVsRunningDocumentTable)) as IVsRunningDocumentTable4;
      if (vsDocTable != null) {
        var runningDocTableEvents = new VsRunningDocTableEvents(vsDocTable, vsEditorAdaptersFactoryService);
        runningDocTableEvents.DocumentOpened += RunningDocTableEventsOnDocumentOpened;
        runningDocTableEvents.DocumentClosed += RunningDocTableEventsOnDocumentClosed;
        runningDocTableEvents.DocumentRenamed += RunningDocTableEventsOnDocumentRenamed;
        _runningDocTableEventsCookie = _runningDocumentTable.Advise(runningDocTableEvents);
      } else {
        Logger.LogWarn("Error getting {0} from Service Provider", typeof(IVsRunningDocumentTable4).FullName);
      }
    }


    public event EventHandler<TextDocumentEventArgs> TextDocumentOpened;
    public event EventHandler<TextDocumentEventArgs> TextDocumentClosed;
    public event EventHandler<VsDocumentRenameEventArgs> TextDocumentRenamed;

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

    public IList<FullPath> GetOpenDocuments() {
      var result = new List<FullPath>();
      foreach (var info in _runningDocumentTable) {
        if (FullPath.IsValid(info.Moniker)) {
          var path = new FullPath(info.Moniker);
          var fi = _fileSystem.GetFileInfoSnapshot(path);
          if (fi.Exists && fi.IsFile) {
            result.Add(path);
          }
        }
      }
      return result;
    }

    protected virtual void Dispose(bool disposing) {
      if (disposing) {
        if (_runningDocTableEventsCookie != 0 && _runningDocumentTable != null) {
          _runningDocumentTable.Unadvise(_runningDocTableEventsCookie);
          _runningDocTableEventsCookie = 0;
        }
      }
    }

    private void RunningDocTableEventsOnDocumentOpened(object sender, VsDocumentEventArgs e) {
      var path = e.Path;
      var info = _runningDocumentTable.GetDocumentInfo(path.Value);

      // Call handlers
      var document = GetTextDocument(info);
      if (document != null) {
        document.FileActionOccurred += TextDocumentOnFileActionOccurred;
        // Add to table
        lock (_openDocumentsLock) {
          _openDocuments[path] = document;
        }
        // Call handlers
        OnTextDocumentOpened(new TextDocumentEventArgs(document));
      }
    }

    private void RunningDocTableEventsOnDocumentClosed(object sender, VsDocumentEventArgs e) {
      var path = e.Path;
      var info = _runningDocumentTable.GetDocumentInfo(path.Value);

      // Call handlers
      var document = GetTextDocument(info);
      if (document != null) {
        OnTextDocumentClosed(new TextDocumentEventArgs(document));
      }

      // Remove from table
      lock (_openDocumentsLock) {
        _openDocuments.Remove(path);
      }
    }

    private void RunningDocTableEventsOnDocumentRenamed(object sender, VsDocumentRenameEventArgs e) {
      lock (_openDocumentsLock) {
        ITextDocument doc;
        if (!_openDocuments.TryGetValue(e.OldPath, out doc)) {
          doc = null;
        }
        if (doc != null) {
          _openDocuments[e.NewPath] = doc;
          _openDocuments.Remove(e.OldPath);
        }
      }

      OnTextDocumentRenamed(e);
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

      ITextDocument document;
      if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document)) {
        return null;
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

    private void TextDocumentOnFileActionOccurred(object sender, TextDocumentFileActionEventArgs args) {
      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        var document = (ITextDocument) sender;

        lock (_openDocumentsLock) {
          if (FullPath.IsValid(args.FilePath)) {
            var newPath = new FullPath(args.FilePath);
            _openDocuments[newPath] = document;
          }

          if (FullPath.IsValid(document.FilePath)) {
            var oldPath = new FullPath(document.FilePath);
            _openDocuments.Remove(oldPath);
          }
        }
      }
    }

    protected virtual void OnTextDocumentOpened(TextDocumentEventArgs e) {
      Logger.WrapActionInvocation(() => { TextDocumentOpened?.Invoke(this, e); });
    }

    protected virtual void OnTextDocumentClosed(TextDocumentEventArgs e) {
      Logger.WrapActionInvocation(() => { TextDocumentClosed?.Invoke(this, e); });
    }

    protected virtual void OnTextDocumentRenamed(VsDocumentRenameEventArgs e) {
      TextDocumentRenamed?.Invoke(this, e);
    }
  }
}