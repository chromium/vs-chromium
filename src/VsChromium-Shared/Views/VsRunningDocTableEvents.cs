// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics.CodeAnalysis;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Editor;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.TextManager.Interop;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Views {
  public class VsRunningDocTableEvents : IVsRunningDocTableEvents3 {
    private readonly IVsRunningDocumentTable4 _runningDocumentTable;
    private readonly IVsEditorAdaptersFactoryService _editorAdaptersFactoryService;

    public VsRunningDocTableEvents(IVsRunningDocumentTable4 runningDocumentTable, IVsEditorAdaptersFactoryService editorAdaptersFactoryService) {
      _runningDocumentTable = runningDocumentTable;
      _editorAdaptersFactoryService = editorAdaptersFactoryService;
    }

    /// <summary>
    /// A document has been opened and loaded in the Running Document Table, or a previously
    /// opened document has been reloaded from disk
    /// </summary>
    public event EventHandler<DocumentLoadedEventArgs> DocumentLoaded;
    /// <summary>
    /// A document has been closed and removed from the Running Document Table
    /// </summary>
    public event EventHandler<DocumentClosedEventArgs> DocumentClosed;
    /// <summary>
    /// A document has been renamed, i.e. the path has changed
    /// </summary>
    public event EventHandler<DocumentRenamedEventArgs> DocumentRenamed;

    #region IVsRunningDocTableEvents3 implementation

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
      // Note: We rely on attribute changes instead, since we want to detect lazy loading of document and projects
      return VSConstants.S_OK;
    }

    public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) {
      // See https://bit.ly/30BFKvJ (Roslyn RunningDocumentTableEventTracker implementation)
      // Either RDTA_DocDataReloaded or RDTA_DocumentInitialized will be triggered if there's a lazy load and the document is now available.
      // See https://devdiv.visualstudio.com/DevDiv/_workitems/edit/937712 for a scenario where we do need the RDTA_DocumentInitialized check.
      // We still check for RDTA_DocDataReloaded because the RDT will mark something as initialized as soon as there is something in the doc data,
      // but that might still not be associated with an ITextBuffer.
      if (IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB.RDTA_DocDataReloaded) || IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB3.RDTA_DocumentInitialized)) {
        var path = PathFromDocCookie(docCookie);
        if (path != null) {
          // The buffer is only set when the document is loaded, ths allows ensuring we call
          // OnDocumentOpened only once per document
          var buffer = TextBufferFromCookie(docCookie);
          if (buffer != null) {
            Logger.LogDebug("Document opened: \"{0}\"", path.Value);
            OnDocumentLoaded(new DocumentLoadedEventArgs(path.Value, buffer));
          }
        }
      }

      // Did we rename?
      if (IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB.RDTA_MkDocument)) {
        if (IsDocumentInitialized(docCookie)) {
          var textBuffer = TextBufferFromCookie(docCookie);
          var oldPath = PathFromMoniker(pszMkDocumentOld);
          var newPath = PathFromMoniker(pszMkDocumentNew);
          if (textBuffer != null && oldPath != null && newPath != null) {
            Logger.LogDebug("Document renamed from \"{0}\" to \"{1}\"", oldPath.Value, newPath.Value);
            OnDocumentRenamed(new DocumentRenamedEventArgs(textBuffer, oldPath.Value, newPath.Value));
          }
        }
      }

      return VSConstants.S_OK;
    }

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public int OnBeforeLastDocumentUnlock(uint docCookie,
                                          uint dwRDTLockType,
                                          uint dwReadLocksRemaining,
                                          uint dwEditLocksRemaining) {
      Logger.WrapActionInvocation(() => {
        // A document is closed once there are no more locks on it
        // See https://bit.ly/30BFKvJ (Roslyn RunningDocumentTableEventTracker implementation)
        if (dwReadLocksRemaining + dwEditLocksRemaining == 0) {
          var path = PathFromDocCookie(docCookie);
          if (path != null) {
              Logger.LogDebug("Document closed: \"{0}\"", path.Value);
              OnDocumentClosed(new DocumentClosedEventArgs(path.Value));
          }
        }
      });
      return VSConstants.S_OK;
    }

    public int OnBeforeSave(uint docCookie) {
      return VSConstants.S_OK;
    }

    public int OnAfterSave(uint docCookie) {
      return VSConstants.S_OK;
    }

    public int OnAfterAttributeChange(uint docCookie, uint grfAttribs) {
      return VSConstants.S_OK;
    }

    public int OnBeforeDocumentWindowShow(uint docCookie, int fFirstShow, IVsWindowFrame pFrame) {
      return VSConstants.S_OK;
    }

    public int OnAfterDocumentWindowHide(uint docCookie, IVsWindowFrame pFrame) {
      return VSConstants.S_OK;
    }

    #endregion

    protected virtual void OnDocumentLoaded(DocumentLoadedEventArgs e) {
      DocumentLoaded?.Invoke(this, e);
    }

    protected virtual void OnDocumentClosed(DocumentClosedEventArgs e) {
      DocumentClosed?.Invoke(this, e);
    }

    protected virtual void OnDocumentRenamed(DocumentRenamedEventArgs e) {
      DocumentRenamed?.Invoke(this, e);
    }

    /// <summary>
    /// Returns <code>true</code> is the document corresponding to the cookie is fully
    /// initialized, i.e. the contents is fully loaded. This is required as the VS
    /// running document table has a notion of "partially initialized" document due to
    /// delay loading.
    /// See https://docs.microsoft.com/en-us/visualstudio/extensibility/internals/delayed-document-loading
    /// </summary>
    private bool IsDocumentInitialized(uint docCookie) {
      var flags = _runningDocumentTable.GetDocumentFlags(docCookie);
      return (flags & (uint)_VSRDTFLAGS4.RDT_PendingInitialization) == 0;
    }

    private FullPath? PathFromMoniker(string moniker) {
      return FullPath.Create(moniker);
    }

    private FullPath? PathFromDocCookie(uint docCookie) {
      if (!IsDocumentInitialized(docCookie)) {
        return null;
      }
      return PathFromMoniker(_runningDocumentTable.GetDocumentMoniker(docCookie));
    }

    private ITextBuffer TextBufferFromCookie(uint docCookie) {
      var bufferAdapter = _runningDocumentTable.GetDocumentData(docCookie) as IVsTextBuffer;
      if (bufferAdapter == null) {
        return null;
      }
      return _editorAdaptersFactoryService.GetDocumentBuffer(bufferAdapter);
    }

    private static bool IsAttributeFlagSet(uint grfAttribs, __VSRDTATTRIB flag) {
      return (grfAttribs & (uint)flag) != 0;
    }

    private static bool IsAttributeFlagSet(uint grfAttribs, __VSRDTATTRIB3 flag) {
      return (grfAttribs & (uint)flag) != 0;
    }
  }

  
  public class DocumentLoadedEventArgs : EventArgs {
    public DocumentLoadedEventArgs(FullPath path, ITextBuffer textBuffer) {
      Path = path;
      TextBuffer = textBuffer;
    }

    public FullPath Path { get; }
    public ITextBuffer TextBuffer { get; }
  }

  public class DocumentClosedEventArgs : EventArgs {
    public DocumentClosedEventArgs(FullPath path) {
      Path = path;
    }

    public FullPath Path { get; }
  }

  public class DocumentRenamedEventArgs : EventArgs {
    public DocumentRenamedEventArgs(ITextBuffer textBuffer, FullPath oldPath, FullPath newPath) {
      TextBuffer = textBuffer;
      OldPath = oldPath;
      NewPath = newPath;
    }

    public ITextBuffer TextBuffer { get; }
    public FullPath OldPath { get; private set; }
    public FullPath NewPath { get; private set; }
  }
}
