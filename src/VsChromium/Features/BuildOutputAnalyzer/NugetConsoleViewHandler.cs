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
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Commands;
using VsChromium.Views;

namespace VsChromium.Features.BuildOutputAnalyzer {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class NugetConsoleViewHandler : IViewHandler {
    private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
    private readonly IOpenDocumentHelper _openDocumentHelper;
    private readonly IBuildOutputParser _buildOutputParser;
    private IWpfTextView _textView;
    private IVsTextView _textViewAdapter;

    [ImportingConstructor]
    public NugetConsoleViewHandler(
      IVsEditorAdaptersFactoryService adaptersFactoryService,
      IOpenDocumentHelper openDocumentHelper,
      IBuildOutputParser buildOutputParser) {
      _adaptersFactoryService = adaptersFactoryService;
      _openDocumentHelper = openDocumentHelper;
      _buildOutputParser = buildOutputParser;
    }

    public int Priority { get { return 100; } }

    public void Attach(IVsTextView textViewAdapter) {
      if (!ApplyToView(textViewAdapter))
        return;

      if (_textViewAdapter != null)
        throw new InvalidOperationException("ViewHandler instance is already attached to a view. Create a new instance?");
      _textViewAdapter = textViewAdapter;
      _textView = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

      var target = new SimpleCommandTarget(
        new CommandID(VSConstants.VSStd2K, (int)VSConstants.VSStd2KCmdID.ECMD_LEFTCLICK),
        Execute,
        HandlesCommand,
        () => true);
      var targetWrapper = new OleCommandTarget("NugetConsoleViewHandler", target);
      _textViewAdapter.AddCommandFilter(targetWrapper, out targetWrapper.NextCommandTarget);
    }

    private bool ApplyToView(IVsTextView textViewAdapter) {
      var textView = _adaptersFactoryService.GetWpfTextView(textViewAdapter);
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
      var caret = _textView.Caret;
      if (caret.InVirtualSpace)
        return null;

      var line = caret.ContainingTextViewLine;
      var extent = line.Extent;
      var result = _buildOutputParser.ParseLine(extent.GetText());
      if (result == null)
        return null;

      var caretLineOffset = caret.Position.BufferPosition - line.Start;
      if (caretLineOffset < result.Index)
        return null;

      if (caretLineOffset >= result.Index + result.Length)
        return null;

      return result;
    }

    private void Execute() {
      var buildOutputSpanForCaret = GetBuildOutputSpanForCaret();
      if (buildOutputSpanForCaret == null)
        return;

      _openDocumentHelper.OpenDocument(buildOutputSpanForCaret.FileName, (vsTextView) => {
        var textView = _adaptersFactoryService.GetWpfTextView(vsTextView);
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
