// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Package;

namespace VsChromium.Views {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentFactoryObserver : IPackagePostInitializer {
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly IFileRegistrationRequestService _fileRegistrationRequestService;

    [ImportingConstructor]
    public TextDocumentFactoryObserver(ITextDocumentFactoryService textDocumentFactoryService, IFileRegistrationRequestService fileRegistrationRequestService) {
      _textDocumentFactoryService = textDocumentFactoryService;
      _fileRegistrationRequestService = fileRegistrationRequestService;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentFactoryService.TextDocumentCreated += TextDocumentFactoryServiceOnTextDocumentCreated;
      _textDocumentFactoryService.TextDocumentDisposed += TextDocumentFactoryServiceOnTextDocumentDisposed;
    }

    /// <summary>
    /// Note: Starting VS 2019 (2017?), this event handler can be called on any thread, not just the UI thread.
    /// For example, when using "Find In Files" feature of VS, which runs on many threads in parallel.
    /// This means the implementation needs to be thread-safe.
    /// </summary>
    private void TextDocumentFactoryServiceOnTextDocumentCreated(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      if (textDocumentEventArgs.TextDocument != null) {
        _fileRegistrationRequestService.RegisterTextDocument(textDocumentEventArgs.TextDocument);
        textDocumentEventArgs.TextDocument.FileActionOccurred += (o, args) => {
          if (args.FileActionType.HasFlag(FileActionTypes.DocumentRenamed)) {
            var document = (ITextDocument)o;
            _fileRegistrationRequestService.RegisterFile(args.FilePath);
            _fileRegistrationRequestService.UnregisterFile(document.FilePath);
          }
        };
      }
    }

    /// <summary>
    /// Note: Starting VS 2019 (2017?), this event handler can be called on any thread, not just the UI thread.
    /// For example, when using "Find In Files" feature of VS, which runs on many threads in parallel.
    /// This means the implementation needs to be thread-safe.
    /// </summary>
    private void TextDocumentFactoryServiceOnTextDocumentDisposed(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      if (textDocumentEventArgs.TextDocument != null) {
        _fileRegistrationRequestService.UnregisterTextDocument(textDocumentEventArgs.TextDocument);
      }
    }
  }
}
