// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
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
  [Guid(GuidList.GuidSourceExplorerToolWindowString)]
  public class SourceExplorerToolWindow : ToolWindowPane, IOleCommandTarget {
    private VsWindowFrameNotifyHandler _frameNotify;

    /// <summary>
    /// Standard constructor for the tool window.
    /// </summary>
    public SourceExplorerToolWindow()
      : base(null) {
      // Set the window title reading it from the resources.
      Caption = Resources.SourceExplorerToolWindowTitle;
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
      ExplorerControl = new SourceExplorerControl();
    }

    public override void OnToolWindowCreated() {
      base.OnToolWindowCreated();
      ExplorerControl.OnVsToolWindowCreated(this);

      // Advise IVsWindowFrameNotify so we know when we get hidden, etc.
      var frame = this.Frame as IVsWindowFrame2;
      if (frame != null) {
        _frameNotify = new VsWindowFrameNotifyHandler(frame);
        _frameNotify.Advise();
      }

      // Hookup command handlers
      var commands = new List<IPackageCommandHandler> {
        new PreviousLocationCommandHandler(this),
        new NextLocationCommandHandler(this),
        new CancelSearchCommandHandler(this),
        new CancelSearchToolWindowCommandHandler(this),
        // Add more here...
      };

      var commandService = (IMenuCommandService)this.GetService(typeof(IMenuCommandService));
      commands.ForEach(handler =>
          commandService.AddCommand(handler.ToOleMenuCommand()));
    }

    public SourceExplorerControl ExplorerControl {
      get { return Content as SourceExplorerControl; }
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
          case SourceExplorerViewModel.DisplayKind.FileNameSearchResult:
          case SourceExplorerViewModel.DisplayKind.TextSearchResult:
            return true;
          default:
            return false;
        }
      }
    }

    public void FocusSearchTextBox(CommandID commandId) {
      switch (commandId.ID) {
        case (int)PkgCmdIdList.CmdidSearchFileNames:
          ExplorerControl.SearchFileNamesCombo.Focus();
          break;
        case (int)PkgCmdIdList.CmdidSearchText:
          ExplorerControl.SearchTextCombo.Focus();
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
      var impl = this.GetService(typeof(IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapQueryStatus(this, impl, ref pguidCmdGroup, cCmds, prgCmds, pCmdText);
    }

    int IOleCommandTarget.Exec(ref System.Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, System.IntPtr pvaIn, System.IntPtr pvaOut) {
      var impl = this.GetService(typeof (IMenuCommandService)) as IOleCommandTarget;
      return OleCommandTargetSpy.WrapExec(this, impl, ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut);
    }

    public void CancelSearch() {
      ExplorerControl.Controller.CancelSearch();
    }
  }
}
