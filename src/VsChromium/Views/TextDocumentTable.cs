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
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly IVsEditorAdaptersFactoryService _vsEditorAdaptersFactoryService;
    private readonly Lazy<bool> _firstRun; 
    private readonly Dictionary<FullPath, ITextDocument> _openDocuments = new Dictionary<FullPath, ITextDocument>();

    [ImportingConstructor]
    public TextDocumentTable(
      [Import(typeof(SVsServiceProvider))]IServiceProvider serviceProvider,
      ITextDocumentFactoryService textDocumentFactoryService,
      IVsEditorAdaptersFactoryService vsEditorAdaptersFactoryService) {
      _serviceProvider = serviceProvider;
      _textDocumentFactoryService = textDocumentFactoryService;
      _vsEditorAdaptersFactoryService = vsEditorAdaptersFactoryService;
      textDocumentFactoryService.TextDocumentCreated += TextDocumentFactoryServiceOnTextDocumentCreated;
      textDocumentFactoryService.TextDocumentDisposed += TextDocumentFactoryServiceOnTextDocumentDisposed;
      _firstRun = new Lazy<bool>(FetchRunningDocumentTable);
    }

    private bool FetchRunningDocumentTable() {
      var rdt = new RunningDocumentTable(_serviceProvider);
      foreach (var info in rdt) {
        // Get doc data
        if (!FullPath.IsValid(info.Moniker))
          continue;

        var path = new FullPath(info.Moniker);
        if (_openDocuments.ContainsKey(path))
          continue;

        // Get vs buffer
        IVsTextBuffer docData = null;
        try {
          docData = info.DocData as IVsTextBuffer;
        }
        catch (Exception e) {
          Logger.LogWarn(e, "Error getting IVsTextBuffer for document {0}, skipping document", path);
        }
        if (docData == null)
          continue;

        // Get ITextDocument
        var textBuffer = _vsEditorAdaptersFactoryService.GetDocumentBuffer(docData);
        if (textBuffer == null)
          continue;

        ITextDocument document;
        if (!_textDocumentFactoryService.TryGetTextDocument(textBuffer, out document))
          continue;

        _openDocuments[path] = document;
      }
      return true;
    }

    private void TextDocumentOnFileActionOccurred(object sender, TextDocumentFileActionEventArgs args) {
      if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
        var document = (ITextDocument)sender;

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

    private void TextDocumentFactoryServiceOnTextDocumentCreated(object sender, TextDocumentEventArgs args) {
      var document = args.TextDocument;
      document.FileActionOccurred += TextDocumentOnFileActionOccurred;
      if (FullPath.IsValid(document.FilePath)) {
        var path = new FullPath(document.FilePath);
        _openDocuments[path] = document;
      }
    }

    private void TextDocumentFactoryServiceOnTextDocumentDisposed(object sender, TextDocumentEventArgs args) {
      var document = args.TextDocument;
      if (FullPath.IsValid(document.FilePath)) {
        var path = new FullPath(document.FilePath);
        _openDocuments.Remove(path);
      }
    }

    public ITextDocument GetOpenDocument(FullPath path) {
      var fetchFromRdtOnce = _firstRun.Value;
      return _openDocuments.GetValue(path);
    }
  }
}