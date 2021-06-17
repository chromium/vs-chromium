// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.ComponentModel.Composition;
using System.IO;
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
using VsChromium.Core.Logging;
using VsChromium.Package;

namespace VsChromium.Views {
  [Export(typeof(IOpenDocumentHelper))]
  class OpenDocumentHelper : IOpenDocumentHelper {
    [Import]
    private IVsEditorAdaptersFactoryService _editorAdaptersFactory = null;

    [Import]
    private IEditorOperationsFactoryService _editorOperationsFactory = null;

    [Import]
    private IOutliningManagerService _outliningManagerService = null;

    [Import(typeof(SVsServiceProvider))]
    private IServiceProvider _serviceProvider = null;

    [Import]
    private IVisualStudioPackageProvider _visualStudioPackageProvider = null;

    public bool OpenDocument(string fileName, Span? span) {
      return OpenDocument(fileName, (_) => span);
    }

    public bool OpenDocument(string fileName, Func<IVsTextView, Span?> spanProvider) {
      try {
        var vsWindowFrame = OpenDocumentInWindowFrame(fileName);
        return NavigateToSpan(vsWindowFrame, spanProvider);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error openning document \"{0}\".", fileName);
        return false;
      }
    }

    public bool OpenDocumentWith(string path, IVsUIHierarchy hierarchy, uint itemid, Func<IVsTextView, Span?> spanProvider) {
      try {
        var openDocument = _serviceProvider.GetService(typeof (IVsUIShellOpenDocument)) as IVsUIShellOpenDocument;
        if (openDocument == null) {
          return OpenDocument(path, spanProvider);
        }

        IVsUIHierarchy ppUIH;
        uint pitemid;
        Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP;
        int pDocInProj;
        var hresult = openDocument.IsDocumentInAProject(
          path,
          out ppUIH,
          out pitemid,
          out ppSP,
          out pDocInProj);
        if (ErrorHandler.Failed(hresult)) {
          Logger.LogHResult(hresult, "Error in IsDocumentInAProject");
          return false;
        }

        if (ppSP == null) {
          ppSP = _visualStudioPackageProvider.Package.InteropServiceProvider;
        }

        if (ppUIH == null) {
          ppUIH = hierarchy;
          pitemid = itemid;
        }

        if (ppUIH == null) {
          return OpenDocument(path, spanProvider);
        }

        var flags = __VSOSEFLAGS.OSE_UseOpenWithDialog;
        //Guid viewGuid = VSConstants.LOGVIEWID_TextView;
        Guid viewGuid = Guid.Empty;
        IVsWindowFrame windowFrame;
        hresult = openDocument.OpenStandardEditor(
          (uint) flags,
          path,
          ref viewGuid,
          Path.GetFileName(path),
          ppUIH,
          pitemid,
          IntPtr.Zero,
          ppSP,
          out windowFrame);
        if (ErrorHandler.Failed(hresult)) {
          Logger.LogHResult(hresult, "Error in OpenStandardEditor");
          return false;
        }

        if (windowFrame != null) {
          hresult = windowFrame.Show();
          if (ErrorHandler.Failed(hresult)) {
          Logger.LogHResult(hresult, "Error in WindowFrame.Show");
          return false;
          }
        }

        return NavigateToSpan(windowFrame, spanProvider);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error openning document \"{0}\".", path);
        return false;
      }
    }

    private IVsWindowFrame OpenDocumentInWindowFrame(string fileName) {
      IVsWindowFrame result;
      IVsUIHierarchy vsUIHierarchy;
      uint num;
      IVsTextView vsTextView;
      VsShellUtilities.OpenDocument(_serviceProvider, fileName, Guid.Empty,
                                    out vsUIHierarchy, out num, out result, out vsTextView);
      return result;
    }

    private bool NavigateToSpan(IVsWindowFrame vsWindowFrame, Func<IVsTextView, Span?> spanProvider) {
      if (vsWindowFrame == null)
        return false;

      var vsTextView = GetVsTextView(vsWindowFrame);
      if (vsTextView == null)
        return false;

      var span = spanProvider(vsTextView);
      if (!span.HasValue)
        return true;

      return NavigateInTextView(vsTextView, span.Value);
    }

    private bool NavigateInTextView(IVsTextView vsTextView, Span span) {
      var wpfTextView = _editorAdaptersFactory.GetWpfTextView(vsTextView);
      var start = new SnapshotPoint(wpfTextView.TextSnapshot, span.Start);
      SelectSpan(wpfTextView, new SnapshotSpan(start, span.Length));
      return true;
    }

    private void SelectSpan(ITextView textView, SnapshotSpan snapshotSpan) {
      var source = textView.BufferGraph.MapUpToSnapshot(snapshotSpan, SpanTrackingMode.EdgeExclusive,
                                                        textView.TextSnapshot);
      var span = source.First<SnapshotSpan>();
      if (_outliningManagerService != null) {
        var outliningManager = _outliningManagerService.GetOutliningManager(textView);
        if (outliningManager != null) {
          outliningManager.ExpandAll(span, (_) => true);
        }
      }
      var virtualSnapshotSpan = new VirtualSnapshotSpan(span);
      _editorOperationsFactory.GetEditorOperations(textView).SelectAndMoveCaret(
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

      if (ErrorHandler.Failed(vsCodeWindow.GetPrimaryView(out vsTextView)))
        return null;

      return vsTextView;
    }
  }
}
