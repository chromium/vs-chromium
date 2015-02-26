// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.ComponentModel.Design;
using System.Text;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text;
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

      var selection = SelectWordOnly(_textView.Selection.StreamSelectionSpan.SnapshotSpan);
      if (string.IsNullOrEmpty(selection))
        return;
      codeSearch.SearchFilePaths(selection);
    }

    public static string SelectWordOnly(SnapshotSpan snapshotSpan) {
      if (snapshotSpan.IsEmpty) {
        var line = snapshotSpan.Snapshot.GetLineFromPosition(snapshotSpan.Start);

        // Extend "start" backward as long as it points to a valid word character
        var start = snapshotSpan.Start;
        // If character at "start" is not a word character, try moving back one and check again.
        // If still not, start is in the middle of nowhere.
        // This is to handle cases like this:
        //   foo bar  <= caret at space between foo and bar, we should select "foo"
        //   foo : test  <= caret at ":", we should not select anything
        if (!IsWordCharacter(start.GetChar())) {
          if (start == line.Start)
            return "";

          start = start - 1;
          if (!IsWordCharacter(start.GetChar()))
            return "";
        }
        var adjustedStart = start;

        while (start > line.Start) {
          var ch = (start - 1).GetChar();
          if (!IsWordCharacter(ch))
            break;
          start = start - 1;
        }

        // Extend "end" forward as long as it points to a valid word character
        var end = adjustedStart;
        while (end < line.End - 1) {
          var ch = (end + 1).GetChar();
          if (!IsWordCharacter(ch))
            break;
          end = end + 1;
        }
        end = end + 1;

        return start.Snapshot.GetText(start, end - start);
      } else {
        // Ensure selection is max. one line.
        var line = snapshotSpan.Snapshot.GetLineFromPosition(snapshotSpan.Start);
        var start = snapshotSpan.Start;
        var end = snapshotSpan.End;
        if (start > end) {
          if (end < line.Start) end = line.Start;
          var temp = start;
          start = end;
          end = temp;
        } else {
          if (end > line.End) end = line.End;
        }
        return snapshotSpan.Snapshot.GetText(start, end - start);
      }
    }

    private static bool IsWordCharacter(char ch) {
      return char.IsLetterOrDigit(ch) ||
             ch == '_';
    }
  }
}
