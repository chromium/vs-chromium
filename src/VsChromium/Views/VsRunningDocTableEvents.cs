// Copyright 2020 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
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
    /// The document has been opened and loaded in the Running Document Table
    /// </summary>
    public event EventHandler<VsDocumentEventArgs> DocumentOpened;
    /// <summary>
    /// The document has been closed and removed from the Running Document Table
    /// </summary>
    public event EventHandler<VsDocumentEventArgs> DocumentClosed;
    /// <summary>
    /// An open document has been renamed, i.e. the path has changed
    /// </summary>
    public event EventHandler<VsDocumentRenameEventArgs> DocumentRenamed;

    #region IVsRunningDocTableEvents3 implementation

    public int OnAfterFirstDocumentLock(uint docCookie, uint dwRDTLockType, uint dwReadLocksRemaining, uint dwEditLocksRemaining) {
      // Note: We rely on attribute changes instead, since we want to detect lazy loading
      return VSConstants.S_OK;
    }

    public int OnAfterAttributeChangeEx(uint docCookie, uint grfAttribs, IVsHierarchy pHierOld, uint itemidOld, string pszMkDocumentOld, IVsHierarchy pHierNew, uint itemidNew, string pszMkDocumentNew) {
      // Either RDTA_DocDataReloaded or RDTA_DocumentInitialized will be triggered if there's a lazy load and the document is now available.
      if (IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB.RDTA_DocDataReloaded) || IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB3.RDTA_DocumentInitialized)) {
        var path = PathFromDocCookie(docCookie);
        if (path != null) {
          // The buffer is only set when the document is loaded, ths allows ensuring we call
          // OnDocumentOpened only once per document
          var buffer = TextBufferFromCookie(docCookie);
          if (buffer != null) {
            Logger.LogDebug("Document opened: \"{0}\"", path.Value);
            OnDocumentOpened(new VsDocumentEventArgs(path.Value));
          }
        }
      }

      // Did we rename?
      if (IsAttributeFlagSet(grfAttribs, __VSRDTATTRIB.RDTA_MkDocument)) {
        if (IsDocumentInitialized(docCookie)) {
          var oldPath = PathFromMoniker(pszMkDocumentOld);
          var newPath = PathFromMoniker(pszMkDocumentNew);
          if (oldPath != null && newPath != null) {
            Logger.LogDebug("Document renamed from \"{0}\" to \"{1}\"", oldPath.Value, newPath.Value);
            OnDocumentRenamed(new VsDocumentRenameEventArgs(oldPath.Value, newPath.Value));
          }
        }
      }

      return VSConstants.S_OK;
    }

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
              OnDocumentClosed(new VsDocumentEventArgs(path.Value));
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

    protected virtual void OnDocumentOpened(VsDocumentEventArgs e) {
      DocumentOpened?.Invoke(this, e);
    }

    protected virtual void OnDocumentClosed(VsDocumentEventArgs e) {
      DocumentClosed?.Invoke(this, e);
    }

    protected virtual void OnDocumentRenamed(VsDocumentRenameEventArgs e) {
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

  public class VsDocumentEventArgs : EventArgs {
    public VsDocumentEventArgs(FullPath path) {
      Path = path;
    }

    public FullPath Path { get; private set; }
  }

  public class VsDocumentRenameEventArgs : EventArgs {
    public VsDocumentRenameEventArgs(FullPath oldPath, FullPath newPath) {
      OldPath = oldPath;
      NewPath = newPath;
    }

    public FullPath OldPath { get; private set; }
    public FullPath NewPath { get; private set; }
  }
}
