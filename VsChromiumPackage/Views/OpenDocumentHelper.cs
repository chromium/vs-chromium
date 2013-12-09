// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Text.Operations;
using Microsoft.VisualStudio.Text.Outlining;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromiumCore;

namespace VsChromiumPackage.Views {
  [Export(typeof(IOpenDocumentHelper))]
  internal class OpenDocumentHelper : IOpenDocumentHelper {
    [Import]
    private IVsEditorAdaptersFactoryService _editorAdaptersFactory = null;

    [Import]
    private IEditorOperationsFactoryService _editorOperationsFactory = null;

    [Import]
    private IOutliningManagerService _outliningManagerService = null;

    [Import(typeof(SVsServiceProvider))]
    private IServiceProvider _serviceProvider = null;

    public bool OpenDocument(string fileName, Span? span) {
      try {
        var vsWindowFrame = OpenDocumentInWindowFrame(fileName);
        if (vsWindowFrame == null)
          return false;

        if (!span.HasValue)
          return true;

        return NavigateInWindowFrame(vsWindowFrame, span.Value);
      }
      catch (Exception e) {
        Logger.LogException(e, "Error openning document \"{0}\".", fileName);
        return false;
      }
    }

    public bool OpenDocument(string fileName) {
      return OpenDocument(fileName, null);
    }

    private IVsWindowFrame OpenDocumentInWindowFrame(string fileName) {
      IVsWindowFrame result;
      IVsUIHierarchy vsUIHierarchy;
      uint num;
      IVsTextView vsTextView;
      VsShellUtilities.OpenDocument(this._serviceProvider, fileName, Guid.Empty,
          out vsUIHierarchy, out num, out result, out vsTextView);
      return result;
    }

    private bool NavigateInWindowFrame(IVsWindowFrame windowFrame, Span span) {
      var vsTextView = GetVsTextView(windowFrame);
      if (vsTextView == null) {
        return false;
      }
      var wpfTextView = this._editorAdaptersFactory.GetWpfTextView(vsTextView);
      var start = new SnapshotPoint(wpfTextView.TextSnapshot, span.Start);
      SelectSpan(wpfTextView, new SnapshotSpan(start, span.Length));
      return true;
    }

    private void SelectSpan(ITextView textView, SnapshotSpan snapshotSpan) {
      var source = textView.BufferGraph.MapUpToSnapshot(snapshotSpan, SpanTrackingMode.EdgeExclusive,
          textView.TextSnapshot);
      var span = source.First<SnapshotSpan>();
      if (this._outliningManagerService != null) {
        var outliningManager = this._outliningManagerService.GetOutliningManager(textView);
        if (outliningManager != null) {
          outliningManager.ExpandAll(span, (_) => true);
        }
      }
      var virtualSnapshotSpan = new VirtualSnapshotSpan(span);
      this._editorOperationsFactory.GetEditorOperations(textView).SelectAndMoveCaret(
          virtualSnapshotSpan.Start, virtualSnapshotSpan.End,
          TextSelectionMode.Stream, EnsureSpanVisibleOptions.AlwaysCenter);
    }

    private static IVsTextView GetVsTextView(IVsWindowFrame windowFrame) {
      if (windowFrame == null)
        throw new ArgumentNullException("windowFrame");

      object obj;
      if (ErrorHandler.Failed(windowFrame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out obj)))
        return null;

      var vsTextView = obj as IVsTextView;
      if (vsTextView != null)
        return vsTextView;

      var vsCodeWindow = obj as IVsCodeWindow;
      if (vsCodeWindow == null)
        return null;

      IVsTextView vsTextView2;
      if (ErrorHandler.Failed(vsCodeWindow.GetPrimaryView(out vsTextView2)))
        return null;

      return vsTextView2;
    }
  }
}
