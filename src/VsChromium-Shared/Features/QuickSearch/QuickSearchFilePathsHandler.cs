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
using VsChromium.Features.ToolWindows;
using VsChromium.Views;

namespace VsChromium.Features.QuickSearch {
  [PartCreationPolicy(CreationPolicy.NonShared)]
  [Export(typeof(IViewHandler))]
  public class QuickSearchFilePathsHandler : IViewHandler {
    private readonly IVsEditorAdaptersFactoryService _adaptersFactoryService;
    private readonly IToolWindowAccessor _toolWindowAccessor;
    private IWpfTextView _textView;
    private IVsTextView _textViewAdapter;

    [ImportingConstructor]
    public QuickSearchFilePathsHandler(
      IVsEditorAdaptersFactoryService adaptersFactoryService,
      IToolWindowAccessor toolWindowAccessor) {
      _adaptersFactoryService = adaptersFactoryService;
      _toolWindowAccessor = toolWindowAccessor;
    }

    public int Priority { get { return 0; } }

    public void Attach(IVsTextView textViewAdapter) {
      if (_textViewAdapter != null)
        throw new InvalidOperationException("ViewHandler instance is already attached to a view. Create a new instance?");

      _textViewAdapter = textViewAdapter;
      _textView = _adaptersFactoryService.GetWpfTextView(textViewAdapter);

      var target = new SimpleCommandTarget(
        new CommandID(GuidList.GuidVsChromiumCmdSet, (int)PkgCmdIdList.CmdidQuickSearchFilePaths),
        Execute);
      var targetWrapper = new OleCommandTarget("QuickSearchFilePaths", target);
      _textViewAdapter.AddCommandFilter(targetWrapper, out targetWrapper.NextCommandTarget);
    }

    private void Execute() {
      var codeSearch = _toolWindowAccessor.CodeSearch;
      if (codeSearch == null)
        return;

      var selection = SelectionHelpers.SelectWordOnly(_textView.Selection.StreamSelectionSpan.SnapshotSpan);
      codeSearch.QuickSearchFilePaths(selection);
    }
  }
}
