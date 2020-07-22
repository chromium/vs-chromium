// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using EnvDTE;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Commands;
using VsChromium.Features.AutoUpdate;
using VsChromium.Package.CommandHandler;
using VsChromium.Wpf;

namespace VsChromium.Features.ToolWindows.CodeSearch {
  /// <summary>
  /// This class implements the tool window exposed by this package and hosts a user control.
  ///
  /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane, 
  /// usually implemented by the package implementer.
  ///
  /// This class derives from the ToolWindowPane class provided from the MPF in order to use its 
  /// implementation of the IVsUIElementPane interface.
  /// </summary>
  [Guid(GuidList.GuidCodeSearchToolWindowString)]
  public class CodeSearchToolWindow : ToolWindowPane, IOleCommandTarget {
    private VsWindowFrameNotifyHandler _frameNotify;
    private IVsTextManager _txtMgr;
    private IComponentModel _componentModel;
    private IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;
    private EnvDTE.WindowEvents WindowEvents { get; set; }

    /// <summary>
    /// Standard constructor for the tool window.
    /// </summary>
    public CodeSearchToolWindow()
      : base(null) {
      // Set the window title reading it from the resources.
      Caption = Resources.CodeSearchToolWindowTitle;
      // Set the image that will appear on the tab of the window frame
      // when docked with an other window
      // The resource ID correspond to the one defined in the resx file
      // while the Index is the offset in the bitmap strip. Each image in
      // the strip being 16x16.
      BitmapResourceID = 301;
      BitmapIndex = 1;

      // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
      // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on 
      // the object returned by the Content property.
      ExplorerControl = new CodeSearchControl();
    }

    private string GetSelectedOrWord()
    {
      string selection = string.Empty;
      IVsTextView vTextView = null;
      int mustHaveFocus = 1;
      _txtMgr.GetActiveView(mustHaveFocus, null, out vTextView);
      if (vTextView == null)
        return selection;
      vTextView.GetSelectedText(out selection);
      if (!selection.Equals(""))
        return selection;
      var textView = _editorAdaptersFactoryService.GetWpfTextView(vTextView);
      var viewadatper = _editorAdaptersFactoryService.GetViewAdapter(textView);
      if (textView == null || viewadatper == null)
        return selection;
      var position = textView.Caret.Position.BufferPosition;
      position = position.TranslateTo(textView.TextSnapshot, PointTrackingMode.Positive);
      TextSpan[] spans = new TextSpan[1];
      var containingline = position.GetContainingLine();
      if (ErrorHandler.Failed(viewadatper.GetWordExtent(containingline.LineNumber, position - containingline.Start, (uint)WORDEXTFLAGS.WORDEXT_CURRENT, spans)))
        return selection;
      var wordstring = string.Empty;
      var span = spans[0];
      vTextView.GetTextStream(span.iStartLine, span.iStartIndex, span.iEndLine, span.iEndIndex, out wordstring);
      return wordstring;
    }

    public override void OnToolWindowCreated() {
      base.OnToolWindowCreated();
      ExplorerControl.OnVsToolWindowCreated(this);
      _txtMgr = (IVsTextManager)GetService(typeof(SVsTextManager));
      _componentModel = (IComponentModel)GetService(typeof(SComponentModel));
      _editorAdaptersFactoryService = (IVsEditorAdaptersFactoryService)_componentModel.GetService<IVsEditorAdaptersFactoryService>();

      EnvDTE.DTE dte = (EnvDTE.DTE)GetService(typeof(EnvDTE.DTE));
      EnvDTE80.Events2 events = (EnvDTE80.Events2)dte.Events;
      this.WindowEvents = (EnvDTE.WindowEvents)events.get_WindowEvents(null);
      this.WindowEvents.WindowActivated += (Window GotFocus, Window LostFocus) => {
        if (GotFocus.ObjectKind.ToString().ToLower().Equals("{" + GuidList.GuidCodeSearchToolWindowString + "}"))
        {
          var word = GetSelectedOrWord();
          if (word.Equals(""))
            return;
          ExplorerControl.SearchCodeCombo.Text = word;
        }
      };
      // Advise IVsWindowFrameNotify so we know when we get hidden, etc.
      var frame = Frame as IVsWindowFrame2;
      if (frame != null) {
        _frameNotify = new VsWindowFrameNotifyHandler(frame);
        _frameNotify.Advise();
      }

      // Hookup command handlers
      var commands = new List<IPackageCommandHandler> {
        new PreviousLocationCommandHandler(this),
        new NextLocationCommandHandler(this),
        new CancelSearchCommandHandler(this),
        //new CancelSearchToolWindowCommandHandler(this),
        // Add more here...
      };

      var commandService = (IMenuCommandService)GetService(typeof(IMenuCommandService));
      commands.ForEach(handler =>
          commandService.AddCommand(handler.ToOleMenuCommand()));
    }

    protected override void Dispose(bool disposing) {
      base.Dispose(disposing);

      if (disposing) {
        if (ExplorerControl.Controller != null) {
          ExplorerControl.Controller.Dispose();
        }
      }
    }

    public CodeSearchControl ExplorerControl {
      get { return Content as CodeSearchControl; }
      set { Content = value; }
    }

    public bool IsVisible {
      get {
        return _frameNotify != null &&
               _frameNotify.IsVisible;
      }
    }

    public bool IsCancelSearchEnabled {
      get {
        switch (ExplorerControl.ViewModel.ActiveDisplay) {
          case CodeSearchViewModel.DisplayKind.SearchFilePathsResult:
          case CodeSearchViewModel.DisplayKind.SearchCodeResult:
            return true;
          default:
            return false;
        }
      }
    }

    public void FocusSearchCodeBox(CommandID commandId) {
      switch (commandId.ID) {
        case (int)PkgCmdIdList.CmdidSearchFilePaths:
          ExplorerControl.SearchFilePathsCombo.Focus();
          break;
        case (int)PkgCmdIdList.CmdidSearchCode:
          ExplorerControl.SearchCodeCombo.Focus();
          break;
      }
    }

    public bool HasNextLocation() {
      return ExplorerControl.Controller.HasNextLocation();
    }

    public bool HasPreviousLocation() {
      return ExplorerControl.Controller.HasPreviousLocation();
    }

    public void NavigateToNextLocation() {
      ExplorerControl.Controller.NavigateToNextLocation();
    }

    public void NavigateToPreviousLocation() {
      ExplorerControl.Controller.NavigateToPreviousLocation();
    }
    public void NotifyPackageUpdate(UpdateInfo updateInfo) {
      WpfUtilities.Post(ExplorerControl, () => {
        ExplorerControl.UpdateInfo = updateInfo;
      });
    }

    int IOleCommandTarget.QueryStatus(ref System.Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, System.IntPtr pCmdText) {
      var impl = GetService(typeof(IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapQueryStatus(this, impl, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    int IOleCommandTarget.Exec(ref System.Guid pguidCmdGroup, uint nCmdId, uint nCmdexecopt, System.IntPtr pvaIn, System.IntPtr pvaOut) {
      var impl = GetService(typeof (IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapExec(this, impl, ref pguidCmdGroup, nCmdId, nCmdexecopt, pvaIn, pvaOut);
    }

    public void CancelSearch() {
      ExplorerControl.Controller.CancelSearch();
    }

    public void QuickSearchCode(string searchPattern) {
      ExplorerControl.Controller.QuickSearchCode(searchPattern);
    }

    public void QuickSearchFilePaths(string searchPattern) {
      ExplorerControl.Controller.QuickFilePaths(searchPattern);
    }

    public void FocusQuickSearchCode() {
      ExplorerControl.Controller.FocusQuickSearchCode();
    }

    public void FocusQuickSearchFilePaths() {
      ExplorerControl.Controller.FocusQuickSearchFilePaths();
    }
  }
}
