// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Text;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumPackage.Package;
using VsChromiumPackage.Threads;

namespace VsChromiumPackage.Features.ChromiumExplorer {
  [Export(typeof(IPackagePostInitializer))]
  public class TextDocumentFactoryObserver : IPackagePostInitializer {
    private readonly ITextDocumentFactoryService _textDocumentFactoryService;
    private readonly IUIRequestProcessor _uiRequestProcessor;

    [ImportingConstructor]
    public TextDocumentFactoryObserver(ITextDocumentFactoryService textDocumentFactoryService, IUIRequestProcessor uiRequestProcessor) {
      _textDocumentFactoryService = textDocumentFactoryService;
      _uiRequestProcessor = uiRequestProcessor;
    }

    public int Priority { get { return 0; } }

    public void Run(IVisualStudioPackage package) {
      _textDocumentFactoryService.TextDocumentCreated += TextDocumentFactoryServiceOnTextDocumentCreated;
      _textDocumentFactoryService.TextDocumentDisposed += TextDocumentFactoryServiceOnTextDocumentDisposed;
    }

    private void TextDocumentFactoryServiceOnTextDocumentCreated(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      var path = textDocumentEventArgs.TextDocument.FilePath;

      if (!IsPhysicalFile(path))
        return;

      var request = new UIRequest {
        Id = "AddFileNameRequest-" + path,
        TypedRequest = new AddFileNameRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private void TextDocumentFactoryServiceOnTextDocumentDisposed(object sender, TextDocumentEventArgs textDocumentEventArgs) {
      var path = textDocumentEventArgs.TextDocument.FilePath;

      if (!IsPhysicalFile(path))
        return;

      var request = new UIRequest {
        Id = "RemoveFileNameRequest-" + path,
        TypedRequest = new RemoveFileNameRequest {
          FileName = path
        }
      };

      _uiRequestProcessor.Post(request);
    }

    private static bool IsPhysicalFile(string path) {
      // This can happen with "Find in files" for example, as it uses a fake filename.
      if (!PathHelpers.IsAbsolutePath(path))
        return false;

      return File.Exists(path);
    }
  }
}
