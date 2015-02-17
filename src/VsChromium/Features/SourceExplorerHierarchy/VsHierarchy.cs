// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using Constants = Microsoft.VisualStudio.OLE.Interop.Constants;

namespace VsChromium.Features.SourceExplorerHierarchy {
  /// <summary>
  /// Implementation of <see cref="IVsUIHierarchy"/> for the virtual project.
  /// </summary>
  public class VsHierarchy : IVsUIHierarchy, IVsProject3, IDisposable {
    private readonly System.IServiceProvider _serviceProvider;
    private readonly IVsGlyphService _vsGlyphService;
    private readonly EventSinkCollection _eventSinks = new EventSinkCollection();
    private readonly VsHierarchyLogger _logger;
    private VsHierarchyNodes _nodes = new VsHierarchyNodes();
    private Microsoft.VisualStudio.OLE.Interop.IServiceProvider _site;
    private uint _selectionEventsCookie;
    private bool _vsHierarchyActive;

    public VsHierarchy(System.IServiceProvider serviceProvider, IVsGlyphService vsGlyphService) {
      _serviceProvider = serviceProvider;
      _vsGlyphService = vsGlyphService;
      _logger = new VsHierarchyLogger(this);
    }

    public event Action<string> OpenDocument;
    public event Action SyncToActiveDocument;

    protected virtual void OnSyncToActiveDocument() {
      var handler = SyncToActiveDocument;
      if (handler != null) handler();
    }

    protected virtual void OnOpenDocument(string obj) {
      var handler = OpenDocument;
      if (handler != null) handler(obj);
    }

    public EventSinkCollection EventSinks {
      get {
        return _eventSinks;
      }
    }

    public IntPtr ImageListPtr {
      get { return _vsGlyphService.ImageListPtr; }
    }

    public void Dispose() {
      Close();
      _nodes.Clear();
    }

    public void Disconnect() {
      CloseVsHierarchy();
    }

    public void Refresh() {
      CloseVsHierarchy();
      SetNodes(_nodes);
    }

    public void SetNodes(VsHierarchyNodes nodes) {
      _nodes = nodes;
      EndRefresh();
    }

    private void EndRefresh() {
      if (_nodes.RootNode.GetChildrenCount() == 0) {
        CloseVsHierarchy();
        return;
      }

      OpenVsHierarchy();
      Redraw();
    }

    private void Redraw() {
      // TODO(rpaquay): Make this more granular.
      foreach (IVsHierarchyEvents vsHierarchyEvents in (IEnumerable)this.EventSinks)
        vsHierarchyEvents.OnInvalidateItems(_nodes.RootNode.ItemId);
      ExpandNode(_nodes.RootNode);
    }

    private void ExpandNode(NodeViewModel node) {
      var uiHierarchyWindow = VsHierarchyUtilities.GetSolutionExplorer(this._serviceProvider);
      if (uiHierarchyWindow == null)
        return;

      uint pdwState;
      if (ErrorHandler.Failed(uiHierarchyWindow.GetItemState(this, node.ItemId, (int)__VSHIERARCHYITEMSTATE.HIS_Expanded, out pdwState)))
        return;

      if (pdwState == (uint)__VSHIERARCHYITEMSTATE.HIS_Expanded)
        return;

      if (ErrorHandler.Failed(uiHierarchyWindow.ExpandItem(this, node.ItemId, EXPANDFLAGS.EXPF_ExpandParentsToShowItem)) ||
          ErrorHandler.Failed(uiHierarchyWindow.ExpandItem(this, node.ItemId, EXPANDFLAGS.EXPF_ExpandFolder))) {
        return;
      }
    }

    private void CloseVsHierarchy() {
      if (!this._vsHierarchyActive)
        return;
      var vsSolution2 = this._serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
      if (vsSolution2 == null || !ErrorHandler.Succeeded(vsSolution2.RemoveVirtualProject((IVsHierarchy)this, 2U)))
        return;
      this._vsHierarchyActive = false;
    }

    private void OpenVsHierarchy() {
      if (_vsHierarchyActive)
        return;
      var vsSolution2 = this._serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
      if (vsSolution2 == null)
        return;
      this.Open();
      int hr = 0;
      if (ErrorHandler.Succeeded(hr)) {
        __VSADDVPFLAGS flags =
          __VSADDVPFLAGS.ADDVP_AddToProjectWindow |
          __VSADDVPFLAGS.ADDVP_ExcludeFromBuild |
          __VSADDVPFLAGS.ADDVP_ExcludeFromCfgUI |
          __VSADDVPFLAGS.ADDVP_ExcludeFromDebugLaunch |
          __VSADDVPFLAGS.ADDVP_ExcludeFromDeploy |
          __VSADDVPFLAGS.ADDVP_ExcludeFromEnumOutputs |
          __VSADDVPFLAGS.ADDVP_ExcludeFromSCC;
        hr = vsSolution2.AddVirtualProject(this, (uint)flags);
        if (hr == VSConstants.VS_E_SOLUTIONNOTOPEN)
          hr = 0;
      }
      if (!ErrorHandler.Succeeded(hr))
        return;
      _vsHierarchyActive = true;
    }

    private void OpenItemDocument(uint itemid) {
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return;
      if (node.Path == null)
        return;
      OnOpenDocument(node.Path);
    }

    private bool FindNode(string name, out NodeViewModel node) {
      node = (NodeViewModel)null;
      if (!string.IsNullOrEmpty(name))
        return _nodes.RootNode.FindNodeByMoniker(name, out node);
      return false;
    }

    public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie) {
      pdwCookie = this._eventSinks.Add((object)pEventSink);
      return 0;
    }

    private void Open() {
      if (!(this is IVsSelectionEvents))
        return;
      IVsMonitorSelection monitorSelection = this._serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
      if (monitorSelection == null)
        return;
      monitorSelection.AdviseSelectionEvents(this as IVsSelectionEvents, out this._selectionEventsCookie);
    }

    public int Close() {
      if ((int)this._selectionEventsCookie != 0) {
        IVsMonitorSelection monitorSelection = this._serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
        if (monitorSelection != null)
          monitorSelection.UnadviseSelectionEvents(this._selectionEventsCookie);
        this._selectionEventsCookie = 0U;
      }
      _nodes.Clear();
      return VSConstants.S_OK;
    }

    public int GetCanonicalName(uint itemid, out string pbstrName) {
      _logger.Log("GetCanonicalName({0})", itemid);
      pbstrName = null;

      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;

      pbstrName = node.Name;
      return 0;
    }

    public int GetGuidProperty(uint itemid, int propid, out Guid pguid) {
      _logger.LogPropertyGuid("GetGuidProperty", itemid, propid);
      pguid = Guid.Empty;
      return VSConstants.E_FAIL;
    }

    public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested) {
      _logger.Log("GetNestedHierarchy({0})", itemid);
      pitemidNested = uint.MaxValue;
      ppHierarchyNested = IntPtr.Zero;
      itemid = 0U;
      return VSConstants.E_FAIL;
    }

    public int GetProperty(uint itemid, int propid, out object pvar) {
      _logger.LogProperty("GetProperty", itemid, propid);
      if (itemid == _nodes.RootNode.ItemId && propid == (int)__VSHPROPID.VSHPROPID_ProjectDir) {
        pvar = "";
        return VSConstants.S_OK;
      }
      NodeViewModel node;
      if (_nodes.FindNode(itemid, out node)) {
        switch (propid) {
          case (int)__VSHPROPID.VSHPROPID_ParentHierarchyItemid:
          case (int)__VSHPROPID.VSHPROPID_ParentHierarchy:
            pvar = null;
            return VSConstants.E_FAIL;
          case (int) __VSHPROPID.VSHPROPID_IconImgList:
            pvar = (int)ImageListPtr;
            return 0;
          default:
            return node.GetProperty(propid, out pvar);
        }
      } else {
        pvar = null;
        return VSConstants.E_NOTIMPL;
      }
    }

    public int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
      _logger.Log("GetSite()");
      site = this._site;
      return 0;
    }

    public int ParseCanonicalName(string pszName, out uint pitemid) {
      _logger.Log("ParseCanonicalName({0})", pszName);
      pitemid = 0U;
      return VSConstants.E_NOTIMPL;
    }

    public int QueryClose(out int pfCanClose) {
      pfCanClose = 1;
      return 0;
    }

    public int SetGuidProperty(uint itemid, int propid, ref Guid rguid) {
      return VSConstants.E_NOTIMPL;
    }

    public int SetProperty(uint itemid, int propid, object var) {
      _logger.LogProperty("SetProperty - ", itemid, propid);
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;
      switch (propid) {
        case -2033:
        case -2032:
          return VSConstants.E_NOTIMPL;
        default:
          return node.SetProperty(propid, var);
      }
    }

    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
      this._site = site;
      return 0;
    }

    public int UnadviseHierarchyEvents(uint dwCookie) {
      this._eventSinks.RemoveAt(dwCookie);
      return 0;
    }

    public int Unused0() {
      return VSConstants.E_NOTIMPL;
    }

    public int Unused1() {
      return VSConstants.E_NOTIMPL;
    }

    public int Unused2() {
      return VSConstants.E_NOTIMPL;
    }

    public int Unused3() {
      return VSConstants.E_NOTIMPL;
    }

    public int Unused4() {
      return VSConstants.E_NOTIMPL;
    }

    public int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      _logger.LogExecCommand(itemid, pguidCmdGroup, nCmdID, nCmdexecopt);

      if ((pguidCmdGroup == VSConstants.GUID_VsUIHierarchyWindowCmds) && nCmdID == (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_DoubleClick) {
        OpenItemDocument(itemid);
        return VSConstants.S_OK;
      }

      if ((pguidCmdGroup == VSConstants.GUID_VsUIHierarchyWindowCmds) && nCmdID == (int)VSConstants.VsUIHierarchyWindowCmdIds.UIHWCMDID_EnterKey) {
        OpenItemDocument(itemid);
        return VSConstants.S_OK;
      }

      if ((pguidCmdGroup == GuidList.GuidVsChromiumCmdSet) && nCmdID == (int)PkgCmdIdList.CmdidSyncToDocument) {
        OnSyncToActiveDocument();
        return VSConstants.S_OK;
      }

      return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    public int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      for (var index = 0; index < cCmds; ++index) {
        _logger.LogQueryStatusCommand(itemid, pguidCmdGroup, prgCmds[index].cmdID);
        if (pguidCmdGroup == VSConstants.VSStd2K && prgCmds[index].cmdID == (int)VSConstants.VSStd2KCmdID.DOUBLECLICK) {
          NodeViewModel node;
          if (_nodes.FindNode(itemid, out node))
            prgCmds[index].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED);
          return VSConstants.S_OK;
        }
      }
      return (int)Constants.OLECMDERR_E_NOTSUPPORTED ;
    }

    public int AddItemFromPackage(string pszItemName, VSADDRESULT[] pResult) {
      return this.AddItem(uint.MaxValue, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, pszItemName, 0U, (string[])null, IntPtr.Zero, pResult);
    }

    public int AddItem(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult) {
      Guid guid = Guid.Empty;
      return this.AddItemWithSpecific(itemidLoc, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, 0U, ref guid, (string)null, ref guid, pResult);
    }

    public int AddItemWithSpecific(uint itemidLoc, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, VSADDRESULT[] pResult) {
      return VSConstants.E_NOTIMPL;
    }

    public int GenerateUniqueItemName(uint itemidLoc, string pszExt, string pszSuggestedRoot, out string pbstrItemName) {
      pbstrItemName = (string)null;
      return VSConstants.E_NOTIMPL;
    }

    public int GetItemContext(uint itemid, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP) {
      _logger.Log("GetItemContext({0})", itemid);
      ppSP = null;
      return 0;
    }

    public int GetMkDocument(uint itemid, out string pbstrMkDocument) {
      _logger.Log("GetMkDocument({0})", itemid);
      pbstrMkDocument = null;
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;
      pbstrMkDocument = node.GetMkDocument();
      return 0;
    }

    public int IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid) {
      _logger.Log("IsDocumentInProject({0})", pszMkDocument);
      pfFound = 0;
      pitemid = 0U;
      if (pdwPriority != null && pdwPriority.Length >= 1)
        pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Unsupported;
      return 0;
    }

#if false
    private int OpenItemViaMiscellaneousProject(uint flags, string moniker, ref Guid rguidLogicalView, out IVsWindowFrame ppWindowFrame) {
      ppWindowFrame = (IVsWindowFrame)null;
      IVsProject3 miscellaneousProject = VsShellUtilities.GetMiscellaneousProject(this._serviceProvider);
      int num = VSConstants.E_FAIL;
      if (miscellaneousProject != null && this._serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument) {
        uint pitemid = uint.MaxValue;
        VSDOCUMENTPRIORITY[] pdwPriority = new VSDOCUMENTPRIORITY[1];
        IVsExternalFilesManager externalFilesManager = this._serviceProvider.GetService(typeof(SVsExternalFilesManager)) as IVsExternalFilesManager;
        externalFilesManager.TransferDocument((string)null, moniker, (IVsWindowFrame)null);
        IVsProject ppProject;
        num = externalFilesManager.GetExternalFilesProject(out ppProject);
        if (ppProject != null) {
          int pfFound;
          num = ppProject.IsDocumentInProject(moniker, out pfFound, pdwPriority, out pitemid);
          if (pfFound == 1 && (int)pitemid != -1)
            num = ppProject.OpenItem(pitemid, ref rguidLogicalView, IntPtr.Zero, out ppWindowFrame);
        }
      }
      return num;
    }
    private void IsDocumentInAnotherProject(string originalPath, out IVsHierarchy hierOpen, out uint itemId, out int isDocInProj) {
      IVsSolution vsSolution = this._serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
      Guid rguidEnumOnlyThisType = Guid.Empty;
      IEnumHierarchies ppenum = (IEnumHierarchies)null;
      itemId = uint.MaxValue;
      hierOpen = (IVsHierarchy)null;
      isDocInProj = 0;
      vsSolution.GetProjectEnum(1U, ref rguidEnumOnlyThisType, out ppenum);
      if (ppenum == null)
        return;
      ppenum.Reset();
      uint pceltFetched = 1U;
      IVsHierarchy[] rgelt = new IVsHierarchy[1];
      ppenum.Next(1U, rgelt, out pceltFetched);
      while ((int)pceltFetched == 1) {
        IVsProject vsProject = rgelt[0] as IVsProject;
        VSDOCUMENTPRIORITY[] pdwPriority = new VSDOCUMENTPRIORITY[1];
        uint pitemid;
        vsProject.IsDocumentInProject(originalPath, out isDocInProj, pdwPriority, out pitemid);
        if (isDocInProj == 1) {
          hierOpen = rgelt[0];
          itemId = pitemid;
          break;
        }
        ppenum.Next(1U, rgelt, out pceltFetched);
      }
    }
#endif

    public int OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      _logger.Log("OpenItem({0})", itemid);
      ppWindowFrame = (IVsWindowFrame)null;
      return VSConstants.E_NOTIMPL;
#if false
      NodeViewModel node = (NodeViewModel)null;
      uint flags = 536936448U;
      int num = 0;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;
      if (node.IsRemote())
        flags |= 32U;
      if (string.IsNullOrEmpty(node.Path))
        return VSConstants.E_NOTIMPL;
      IVsUIHierarchy hierarchy = (IVsUIHierarchy)null;
      IVsHierarchy hierOpen = (IVsHierarchy)null;
      int isDocInProj = 0;
      uint itemid1;
      if (!VsShellUtilities.IsDocumentOpen(this._serviceProvider, node.Path, rguidLogicalView, out hierarchy, out itemid1, out ppWindowFrame)) {
        this.IsDocumentInAnotherProject(node.OriginalPath, out hierOpen, out itemid1, out isDocInProj);
        if (hierOpen == null) {
          num = this.OpenItemViaMiscellaneousProject(flags, node.OriginalPath, ref rguidLogicalView, out ppWindowFrame);
        } else {
          IVsProject3 vsProject3 = hierOpen as IVsProject3;
          num = vsProject3 == null ? this.OpenItemViaMiscellaneousProject(flags, node.OriginalPath, ref rguidLogicalView, out ppWindowFrame) : vsProject3.OpenItem(itemid1, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }
      }
      if (ppWindowFrame != null)
        num = ppWindowFrame.Show();
      return num;
#endif
    }

    public int OpenItemWithSpecific(uint itemid, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      return OpenItem(itemid, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
    }

    public int RemoveItem(uint dwReserved, uint itemid, out int pfResult) {
      pfResult = 0;
      return VSConstants.E_NOTIMPL;
    }

    public int ReopenItem(uint itemid, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      ppWindowFrame = (IVsWindowFrame)null;
      return VSConstants.E_NOTIMPL;
    }

    public int TransferItem(string pszMkDocumentOld, string pszMkDocumentNew, IVsWindowFrame punkWindowFrame) {
      return 0;
    }
  }
}
