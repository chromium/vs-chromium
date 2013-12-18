// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromiumPackage.Commands;
using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Features.BuildErrors {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class NugetConsoleViewHandler : IViewHandler {
    [Import]
    internal IUIRequestProcessor RequestProcessor = null; // Set via MEF.

    [Import]
    internal IVsEditorAdaptersFactoryService AdapterService = null; // Set via MEF

    [Import]
    internal IViewTagAggregatorFactoryService TagAggregatorFactory = null; // Set via MEF

    [Import]
    internal IOpenDocumentHelper OpenDocumentHelper = null; // Set via MEF

    [Import]
    internal IBuildOutputParser BuildOutputParser = null; // Set via MEF

    private IWpfTextView _textView;
    private IVsTextView _textViewAdapter;

    public void Attach(IVsTextView textViewAdapter) {
      if (!ApplyToView(textViewAdapter))
        return;

      if (_textViewAdapter != null)
        throw new InvalidOperationException("ViewHandler instance is already attached to a view. Create a new instance?");
      _textViewAdapter = textViewAdapter;
      _textView = AdapterService.GetWpfTextView(textViewAdapter);

      var target = new SimpleCommandTarget(new CommandID(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.DOUBLECLICK), Execute, HandlesCommand);
      var targetWrapper = new OleCommandTarget(target);
      _textViewAdapter.AddCommandFilter(targetWrapper, out targetWrapper.NextCommandTarget);
    }

    private bool ApplyToView(IVsTextView textViewAdapter) {
      var textView = AdapterService.GetWpfTextView(textViewAdapter);
      if (textView == null)
        return false;

      var textBuffer = textView.TextBuffer;
      if (textBuffer == null)
        return false;

      return textBuffer.ContentType.IsOfType(NugetConsoleViewConstants.ContentType);
    }

    private bool HandlesCommand() {
      var buildOutputSpan = GetBuildOutputSpanForCaret();
      return buildOutputSpan != null;
    }

    private BuildOutputSpan GetBuildOutputSpanForCaret() {
      var line = _textView.Caret.ContainingTextViewLine;
      var extent = line.Extent;
      return BuildOutputParser.ParseLine(extent.GetText());
    }

    private void Execute() {
      var buildOutputSpanForCaret = GetBuildOutputSpanForCaret();
      if (buildOutputSpanForCaret == null)
        return;

      OpenDocumentHelper.OpenDocument(buildOutputSpanForCaret.FileName, (vsTextView) => {
        var textView = AdapterService.GetWpfTextView(vsTextView);
        if (textView == null)
          return null;

        var snapshot = textView.TextBuffer.CurrentSnapshot;
        if (buildOutputSpanForCaret.LineNumber < 0 || buildOutputSpanForCaret.LineNumber >= snapshot.LineCount)
          return null;

        var line = snapshot.GetLineFromLineNumber(buildOutputSpanForCaret.LineNumber);
        if (buildOutputSpanForCaret.ColumnNumber < 0 || buildOutputSpanForCaret.ColumnNumber >= line.Length)
          return new Span(line.Start, 0);

        return new Span(line.Start + buildOutputSpanForCaret.ColumnNumber, 0);
      });
    }
  }
}
