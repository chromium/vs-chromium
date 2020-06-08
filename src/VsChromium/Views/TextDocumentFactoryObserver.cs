// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Package;

namespace VsChromium.Views {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentFactoryObserver : IPackagePostInitializer {
    private readonly ITextDocumentTable _textDocumentTable;
    private readonly IFileRegistrationRequestService _fileRegistrationRequestService;

    [ImportingConstructor]
    public TextDocumentFactoryObserver(ITextDocumentTable textDocumentTable, IFileRegistrationRequestService fileRegistrationRequestService) {
      _textDocumentTable = textDocumentTable;
      _fileRegistrationRequestService = fileRegistrationRequestService;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentTable.OpenDocumentCreated += TextDocumentTable_DocumentOpened;
      _textDocumentTable.OpenDocumentClosed += TextDocumentTable_DocumentClosed;
      _textDocumentTable.OpenDocumentRenamed += TextDocumentTable_DocumentRenamed;
    }

    private void TextDocumentTable_DocumentOpened(object sender, OpenDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.RegisterTextDocument(textDocumentEventArgs.OpenDocument.TextDocument);
    }

    private void TextDocumentTable_DocumentClosed(object sender, OpenDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.UnregisterTextDocument(textDocumentEventArgs.OpenDocument.TextDocument);
    }

    private void TextDocumentTable_DocumentRenamed(object sender, OpenDocumentRenamedEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.UnregisterFile(textDocumentEventArgs.OldPath.Value);
      _fileRegistrationRequestService.RegisterFile(textDocumentEventArgs.OpenDocument.Path.Value);
    }
  }
}
