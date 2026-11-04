// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Shell;
using VsChromium.Core.Logging;
using VsChromium.Package;
using VsChromium.ServerProxy;

namespace VsChromium.Views {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentRegistrationManager : IPackagePostInitializer {
    private readonly ITextDocumentTable _textDocumentTable;
    private readonly IFileRegistrationRequestService _fileRegistrationRequestService;
    private readonly IServerClient _serverClient;

    [ImportingConstructor]
    public TextDocumentRegistrationManager(ITextDocumentTable textDocumentTable, IFileRegistrationRequestService fileRegistrationRequestService,
                                           IServerClient serverClient) {
      _textDocumentTable = textDocumentTable;
      _fileRegistrationRequestService = fileRegistrationRequestService;
      _serverClient = serverClient;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentTable.TextDocumentOpened += TextDocumentFactoryServiceOnTextDocumentOpened;
      _textDocumentTable.TextDocumentClosed += TextDocumentFactoryServiceOnTextDocumentClosed;
      _textDocumentTable.TextDocumentRenamed += TextTextDocumentFactoryServiceOnTextDocumentRenamed;
      _serverClient.ProcessStarted += ServerClientOnProcessStarted;
    }

    /// <summary>
    /// After a restart of the server (e.g. from the server dashboard), the open
    /// documents are registered again with the new server (it discovers their
    /// projects).
    /// </summary>
    private void ServerClientOnProcessStarted(object sender, EventArgs e) {
      if (_serverClient.StartCount <= 1) {
        return;
      }
      RegisterOpenDocumentsAsync().FileAndForget("VsChromium/TextDocuments/RegisterAfterRestart");
    }

    private async System.Threading.Tasks.Task RegisterOpenDocumentsAsync() {
      await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
      foreach (var document in _textDocumentTable.GetOpenDocuments()) {
        Logger.WrapActionInvocation(() => _fileRegistrationRequestService.RegisterTextDocument(document));
      }
    }

    private void TextDocumentFactoryServiceOnTextDocumentOpened(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.RegisterTextDocument(textDocumentEventArgs.TextDocument);
    }

    private void TextDocumentFactoryServiceOnTextDocumentClosed(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      _fileRegistrationRequestService.UnregisterTextDocument(textDocumentEventArgs.TextDocument);
    }

    private void TextTextDocumentFactoryServiceOnTextDocumentRenamed(object sender, TextDocumentRenamedEventArgs e) {
      _fileRegistrationRequestService.RegisterFile(e.NewPath.Value);
      _fileRegistrationRequestService.UnregisterFile(e.OldPath.Value);
    }
  }
}
