// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Commands;
using VsChromium.Views;

namespace VsChromium.Features.FormatComment {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class FormatCommentHandler : IViewHandler {
    private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
    private readonly ICommentFormatter _commentFormatter;
    private IWpfTextView _textView;
    private IVsTextView _textViewAdapter;

    [ImportingConstructor]
    public FormatCommentHandler(
      IVsEditorAdaptersFactoryService adaptersFactoryService,
      ICommentFormatter commentFormatter) {
      _adaptersFactoryService = adaptersFactoryService;
      _commentFormatter = commentFormatter;
    }

    public int Priority { get { return 0; } }

    public void Attach(IVsTextView textViewAdapter) {
      if (_textViewAdapter != null)
        throw new InvalidOperationException("ViewHandler instance is already attached to a view. Create a new instance?");
      _textViewAdapter = textViewAdapter;
      _textView = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

      var target = new SimpleCommandTarget(new CommandID(GuidList.GuidVsChromiumCmdSet, PkgCmdIdList.CmdidFormatComment), Execute);
      var targetWrapper = new OleCommandTarget(target);
      _textViewAdapter.AddCommandFilter(targetWrapper, out targetWrapper.NextCommandTarget);
    }

    private void Execute() {
      var extendSpanResult = _commentFormatter.ExtendSpan(_textView.Selection.StreamSelectionSpan.SnapshotSpan);
      if (extendSpanResult == null)
        return;

      var result = _commentFormatter.FormatLines(extendSpanResult);
      if (result == null)
        return;

      using (var edit = _textView.TextBuffer.CreateEdit()) {
        if (_commentFormatter.ApplyChanges(edit, result))
          edit.Apply();
      }
    }
  }
}
