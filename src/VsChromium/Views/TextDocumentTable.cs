// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  [Export(typeof(ITextDocumentTable))]
  public class TextDocumentTable : ITextDocumentTable {
    private readonly IServiceProvider _serviceProvider;
    private readonly IFileSystem _fileSystem;
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;
    private readonly Lazy<bool> _firstRun;
    private readonly object _openDocumentsLock = new object();
    private readonly Dictionary<FullPath, ITextDocument> _openDocuments = new Dictionary<FullPath, ITextDocument>();

    public event EventHandler<OpenDocumentEventArgs> OpenDocumentCreated;
    public event EventHandler<OpenDocumentEventArgs> OpenDocumentClosed;
    public event EventHandler<OpenDocumentEventArgs> OpenDocumentSavedToDisk;
    public event EventHandler<OpenDocumentEventArgs> OpenDocumentLoadedFromDisk;
    public event EventHandler<OpenDocumentRenamedEventArgs> OpenDocumentRenamed;

    [ImportingConstructor]
    public TextDocumentTable(
      [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
      IFileSystem fileSystem,
      ITextDocumentFactoryService textDocumentFactoryService,
      IVsEditorAdaptersFactoryService vsEditorAdaptersFactoryService) {
      _serviceProvider = serviceProvider;
      _fileSystem = fileSystem;
      _textDocumentFactoryService = textDocumentFactoryService;
      _vsEditorAdaptersFactoryService = vsEditorAdaptersFactoryService;
      textDocumentFactoryService.TextDocumentCreated += TextDocumentFactoryServiceOnTextDocumentCreated;
      textDocumentFactoryService.TextDocumentDisposed += TextDocumentFactoryServiceOnTextDocumentDisposed;
      _firstRun = new Lazy<bool>(FetchRunningDocumentTable);
    }

    public OpenDocument GetOpenDocument(FullPath path) {
      lock (_openDocumentsLock) {
        var fetchFromRdtOnce = _firstRun.Value;
        var textDoc = _openDocuments.GetValue(path);
        if (textDoc == null) {
          return null;
        } else {
          return new OpenDocument(path, textDoc);
        }
      }
    }

    public IList<OpenDocument> GetOpenDocuments() {
      var result = new List<OpenDocument>();
      var rdt = new RunningDocumentTable(_serviceProvider);
      foreach (var info in rdt) {
        var textDocument = GetTextDocumentFromRTDEntry(info);
        if (textDocument != null) {
          result.Add(textDocument);
        }
      }
      return result;
    }

    private bool FetchRunningDocumentTable() {
      var rdt = new RunningDocumentTable(_serviceProvider);
      foreach (var info in rdt) {
        var textDocument = GetTextDocumentFromRTDEntry(info);
        if (textDocument != null) {
          lock (_openDocumentsLock) {
            _openDocuments[textDocument.Path] = textDocument.TextDocument;
          }
        }
      }
      return true;
    }

    private OpenDocument GetTextDocumentFromRTDEntry(RunningDocumentInfo info) {
      // Get doc data
      if (!FullPath.IsValid(info.Moniker)) {
        return null;
      }
      var path = new FullPath(info.Moniker);

      // Get vs buffer
      IVsTextBuffer docData = null;
      try {
        docData = info.DocData as IVsTextBuffer;
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error getting IVsTextBuffer for document {0}, skipping document", path);
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
      return new OpenDocument(path, document);
    }

    /// <summary>
    /// Note: Starting VS 2019 (2017?), this event handler can be called on any thread, not just the UI thread.
    /// For example, when using "Find In Files" feature of VS, which runs on many threads in parallel.
    /// This means the implementation needs to be thread-safe.
    /// </summary>
    private void TextDocumentFactoryServiceOnTextDocumentCreated(object sender, Microsoft.VisualStudio.Text.TextDocumentEventArgs args) {
      var document = args.TextDocument;
      document.FileActionOccurred += TextDocumentOnFileActionOccurred;
      if (FullPath.IsValid(document.FilePath)) {
        var path = new FullPath(document.FilePath);
        lock (_openDocumentsLock) {
          _openDocuments[path] = document;
        }
        OnDocumentCreated(new OpenDocumentEventArgs(new OpenDocument(path, document)));
      }
    }

    /// <summary>
    /// Note: Starting VS 2019 (2017?), this event handler can be called on any thread, not just the UI thread.
    /// For example, when using "Find In Files" feature of VS, which runs on many threads in parallel.
    /// This means the implementation needs to be thread-safe.
    /// </summary>
    private void TextDocumentFactoryServiceOnTextDocumentDisposed(object sender, Microsoft.VisualStudio.Text.TextDocumentEventArgs args) {
      var document = args.TextDocument;
      if (FullPath.IsValid(document.FilePath)) {
        var path = new FullPath(document.FilePath);
        lock (_openDocumentsLock) {
          _openDocuments.Remove(path);
        }
        OnDocumentClosed(new OpenDocumentEventArgs(new OpenDocument(path, document)));
      }
    }

    private void TextDocumentOnFileActionOccurred(object sender, TextDocumentFileActionEventArgs args) {
      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        var document = (ITextDocument)sender;

        FullPath? newPath = ValidatePath(args.FilePath);
        FullPath? oldPath = ValidatePath(document.FilePath);

        // Common case: rename of valid file to valid file
        if (newPath != null && oldPath != null) {
          lock (_openDocumentsLock) {
            _openDocuments.Remove(oldPath.Value);
            _openDocuments[newPath.Value] = document;
          }
          OnDocumentRenamed(new OpenDocumentRenamedEventArgs(oldPath.Value, new OpenDocument(newPath.Value, document)));
        }

        // Rename from invalid file to valid file
        if (newPath != null && oldPath == null) {
          lock (_openDocumentsLock) {
            _openDocuments[newPath.Value] = document;
          }
          OnDocumentCreated(new OpenDocumentEventArgs(new OpenDocument(newPath.Value, document)));
        }

        // Rename from valid file to invalid file
        if (newPath == null && oldPath != null) {
          lock (_openDocumentsLock) {
            _openDocuments.Remove(oldPath.Value);
          }
          OnDocumentClosed(new OpenDocumentEventArgs(new OpenDocument(oldPath.Value, document)));
        }
      }
    }

    private FullPath? ValidatePath(string filePath) {
      if (FullPath.IsValid(filePath)) {
        return new FullPath(filePath);
      }
      return null;
    }

    protected virtual void OnDocumentCreated(OpenDocumentEventArgs e) {
      OpenDocumentCreated?.Invoke(this, e);
    }

    protected virtual void OnDocumentClosed(OpenDocumentEventArgs e) {
      OpenDocumentClosed?.Invoke(this, e);
    }

    protected virtual void OnDocumentSavedToDisk(OpenDocumentEventArgs e) {
      OpenDocumentSavedToDisk?.Invoke(this, e);
    }

    protected virtual void OnOpenDocumentLoadedFromDisk(OpenDocumentEventArgs e) {
      OpenDocumentLoadedFromDisk?.Invoke(this, e);
    }

    protected virtual void OnDocumentRenamed(OpenDocumentRenamedEventArgs e) {
      OpenDocumentRenamed?.Invoke(this, e);
    }
  }
}
