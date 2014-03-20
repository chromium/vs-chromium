// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using VsChromium.Package;
using VsChromium.Views;

namespace VsChromium.Features.ChromiumExplorer {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentFactoryObserver : IPackagePostInitializer {
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly ITextDocumentService _textDocumentService;

    [ImportingConstructor]
    public TextDocumentFactoryObserver(ITextDocumentFactoryService textDocumentFactoryService, ITextDocumentService textDocumentService) {
      _textDocumentFactoryService = textDocumentFactoryService;
      _textDocumentService = textDocumentService;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentFactoryService.TextDocumentCreated += TextDocumentFactoryServiceOnTextDocumentCreated;
      _textDocumentFactoryService.TextDocumentDisposed += TextDocumentFactoryServiceOnTextDocumentDisposed;
    }

    private void TextDocumentFactoryServiceOnTextDocumentCreated(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _textDocumentService.OnDocumentOpen(textDocumentEventArgs.TextDocument);
    }

    private void TextDocumentFactoryServiceOnTextDocumentDisposed(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _textDocumentService.OnDocumentClose(textDocumentEventArgs.TextDocument);
    }
  }
}
