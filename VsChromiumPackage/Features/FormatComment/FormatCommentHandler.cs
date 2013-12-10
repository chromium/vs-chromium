// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Tagging;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromiumPackage.Commands;
using VsChromiumPackage.Threads;
using VsChromiumPackage.Views;

namespace VsChromiumPackage.Features.FormatComment {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class FormatCommentHandler : IViewHandler {
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

      var target = new SimpleCommandTarget(new CommandID(GuidList.GuidVsChromiumCmdSet, PkgCmdIdList.CmdidFormatComment), Execute);
      var targetWrapper = new CommandTargetWrapper(target);
      _textViewAdapter.AddCommandFilter(targetWrapper, out targetWrapper.NextCommandTarget);
    }

    private void Execute() {
      var commentFormatter = new CommentFormatter();
      var lines = commentFormatter.ExtendSpan(_textView.Selection.StreamSelectionSpan.SnapshotSpan);
      if (lines == null)
        return;

      var result = commentFormatter.FormatLines(lines.Item1, lines.Item2);
      if (result == null)
        return;

      using (var edit = _textView.TextBuffer.CreateEdit()) {
        if (commentFormatter.ApplyChanges(edit, result))
          edit.Apply();
      }
    }
  }
}
