// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Features.ChromiumExplorer {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class ViewHandler : IViewHandler {
    [Import]
    internal IUIRequestProcessor RequestProcessor = null; // Set via MEF.

    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null; // Set via MEF

    [Import]
    internal IViewTagAggregatorFactoryService TagAggregatorFactory = null; // Set via MEF

    private IWpfTextView _textView;
    private IVsTextView _textViewAdapter;

    public void Attach(IVsTextView textViewAdapter) {
      if (_textViewAdapter != null)
        throw new InvalidOperationException("ViewHandler instance is already attached to a view. Create a new instance?");
      _textViewAdapter = textViewAdapter;
      _textView = AdapterService.GetWpfTextView(textViewAdapter);

      GetTextDocuments(_textView).ForAll(document => ProcessDocument(document));
    }

    private void ProcessDocument(ITextDocument document) {
      var path = document.FilePath;

      // This can happen with "Find in files" for example, as it uses a fake filename.
      if (!PathHelpers.IsAbsolutePath(path))
        return;

      if (!File.Exists(path))
        return;

      var request = new UIRequest {
        Id = "AddFileNameRequest-" + path,
        TypedRequest = new AddFileNameRequest {
          FileName = path
        }
      };

      RequestProcessor.Post(request);
    }

    /// <summary>
    /// Return all the ITextDocument instances in the buffer graph associated to "textView".
    /// </summary>
    private IEnumerable<ITextDocument> GetTextDocuments(ITextView textView) {
      foreach (var buffer in textView.BufferGraph.GetTextBuffers(_ => true)) {
        ITextDocument document;
        if (buffer.Properties.TryGetProperty(typeof(ITextDocument), out document))
          yield return document;
      }
    }
  }
}
