// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Package;

namespace VsChromium.Views {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentRegistrationManager : IPackagePostInitializer {
    private readonly ITextDocumentTable _textDocumentTable;
    private readonly IFileRegistrationRequestService _fileRegistrationRequestService;

    [ImportingConstructor]
    public TextDocumentRegistrationManager(ITextDocumentTable textDocumentTable, IFileRegistrationRequestService fileRegistrationRequestService) {
      _textDocumentTable = textDocumentTable;
      _fileRegistrationRequestService = fileRegistrationRequestService;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentTable.TextDocumentOpened += TextDocumentFactoryServiceOnTextDocumentOpened;
      _textDocumentTable.TextDocumentClosed += TextDocumentFactoryServiceOnTextDocumentClosed;
      _textDocumentTable.TextDocumentRenamed += TextTextDocumentFactoryServiceOnTextDocumentRenamed;
    }

    private void TextDocumentFactoryServiceOnTextDocumentOpened(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.RegisterTextDocument(textDocumentEventArgs.TextDocument);
    }

    private void TextDocumentFactoryServiceOnTextDocumentClosed(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.UnregisterTextDocument(textDocumentEventArgs.TextDocument);
    }

    private void TextTextDocumentFactoryServiceOnTextDocumentRenamed(object sender, VsDocumentRenameEventArgs e) {
      _fileRegistrationRequestService.RegisterFile(e.NewPath.Value);
      _fileRegistrationRequestService.UnregisterFile(e.OldPath.Value);
    }
  }
}
