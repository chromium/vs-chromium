// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Commands;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
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
    private readonly Dictionary<CommandID, VsHierarchyCommandHandler> _commandHandlers = new Dictionary<CommandID, VsHierarchyCommandHandler>();

    private VsHierarchyNodes _nodes = new VsHierarchyNodes();
    private Microsoft.VisualStudio.OLE.Interop.IServiceProvider _site;
    private uint _selectionEventsCookie;
    private bool _vsHierarchyActive;

    public VsHierarchy(System.IServiceProvider serviceProvider, IVsGlyphService vsGlyphService) {
      _serviceProvider = serviceProvider;
      _vsGlyphService = vsGlyphService;
      _logger = new VsHierarchyLogger(this);
    }

    public EventSinkCollection EventSinks {
      get {
        return _eventSinks;
      }
    }

    public IntPtr ImageListPtr {
      get { return _vsGlyphService.ImageListPtr; }
    }

    public VsHierarchyNodes Nodes {
      get { return _nodes; }
    }

    public void AddCommandHandler(VsHierarchyCommandHandler handler) {
      _commandHandlers.Add(handler.CommandId, handler);
    }

    public void Dispose() {
      Close();
    }

    public void Disconnect() {
      CloseVsHierarchy();
    }

    public void Reconnect() {
      CloseVsHierarchy();
      SetNodes(_nodes, null);
    }

    public void SetNodes(VsHierarchyNodes nodes, VsHierarchyChanges changes) {
      var description = string.Format("SetNodes(node count={0}, added={1}, deleted={2})",
        nodes.Count,
        (changes == null ? -1 : changes.AddedItems.Count),
        (changes == null ? -1 : changes.DeletedItems.Count));
      using (new TimeElapsedLogger(description)) {
        // Simple case: empty hiererchy
        if (nodes.RootNode.GetChildrenCount() == 0) {
          _nodes = nodes;
          CloseVsHierarchy();
          return;
        }

        // Simple case of unknwon changes or hierarchy is not active.
        if (changes == null || !_vsHierarchyActive) {
          _nodes = nodes;
          OpenVsHierarchy();
          RefreshAll();
          return;
        }

        // Incremental case: Notify of add/remove items.
        Debug.Assert(_vsHierarchyActive);
        Debug.Assert(changes != null);

        // Note: We want to avoid calling "OnItemAdded" in "IVsHierarchyEvents"
        // if possible, because it implies making the item visible.
        // "IVsHierarchyEvents" supports a version of "OnItemAdded" with a
        // "ensureVisible" flag.
        var events1 = EventSinks.OfType<IVsHierarchyEvents>().ToList();
        var events2 = events1.OfType<IVsHierarchyEvents2>().ToList();
        var events1Only = events1.Except(events2.OfType<IVsHierarchyEvents>()).ToList();

        // Pass 1: Notify deletion of old items as long as we have the old node
        // collection active. This is safe because the hierarchy host at this
        // point knows only about current nodes, and does not know anything about
        // new nodes. In return, we have not updated out "_nodes" member, so any
        // GetProperty call will return info about current node only.
        foreach (var deleted in changes.DeletedItems) {
          var deletedNode = _nodes.GetNode(deleted);
          Debug.Assert(deletedNode != null);
          Debug.Assert(deletedNode.Parent != null);
          if (_logger.IsLogDiffEnabled) {
            _logger.Log("Deleting node {0,7}-\"{1}\"", deletedNode.ItemId, deletedNode.Path);
          }

          // PERF: avoid allocation
          for(var i = 0; i < events1.Count; i++){
            events1[i].OnItemDeleted(deletedNode.ItemId);
          }
        }

        // Pass 2: Notify of node additions. We first need to switch our "_nodes"
        // field to the new node collection, so that any query made by the
        // hierarchy host as a result of add events will be answered with the
        // right set of nodes (the new ones).
        _nodes = nodes;
        foreach (var added in changes.AddedItems) {
          var addedNode = nodes.GetNode(added);
          var previousSiblingItemId = addedNode.GetPreviousSiblingItemId();
          Debug.Assert(addedNode != null);
          Debug.Assert(addedNode.Parent != null);
          if (_logger.IsLogDiffEnabled) {
            _logger.Log("Adding node {0,7}-\"{1}\"", addedNode.ItemId, addedNode.Path);
            _logger.Log("   child of {0,7}-\"{1}\"", addedNode.Parent.ItemId, addedNode.Parent.Path);
            _logger.Log(
              "    next to {0,7}-\"{1}\"",
              previousSiblingItemId,
              (previousSiblingItemId != VSConstants.VSITEMID_NIL
                ? nodes.GetNode(previousSiblingItemId).Path
                : "nil"));
          }

          // PERF: avoid allocation
          for (var i = 0; i < events1Only.Count; i++) {
            events1Only[i].OnItemAdded(
              addedNode.Parent.ItemId,
              previousSiblingItemId,
              addedNode.ItemId);
          }

          // PERF: avoid allocation
          for (var i = 0; i < events2.Count; i++) {
            events2[i].OnItemAdded(
              addedNode.Parent.ItemId,
              previousSiblingItemId,
              addedNode.ItemId,
              false /* ensure visible */);
          }
        }
      }
    }

    public void SelectNode(NodeViewModel node) {
      var uiHierarchyWindow = VsHierarchyUtilities.GetSolutionExplorer(_serviceProvider);
      if (uiHierarchyWindow == null)
        return;

      if (ErrorHandler.Failed(uiHierarchyWindow.ExpandItem(this, node.ItemId, EXPANDFLAGS.EXPF_SelectItem))) {
        Logger.LogError("Error selecting item in solution explorer.");
      }
    }

    private void RefreshAll() {
      foreach (IVsHierarchyEvents vsHierarchyEvents in EventSinks) {
        vsHierarchyEvents.OnInvalidateItems(_nodes.RootNode.ItemId);
      }
      ExpandNode(_nodes.RootNode);
    }

    private void ExpandNode(NodeViewModel node) {
      var uiHierarchyWindow = VsHierarchyUtilities.GetSolutionExplorer(_serviceProvider);
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

    private void OpenVsHierarchy() {
      if (_vsHierarchyActive)
        return;
      var vsSolution2 = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
      if (vsSolution2 == null)
        return;
      Open();
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

    private void CloseVsHierarchy() {
      if (!_vsHierarchyActive)
        return;
      var vsSolution2 = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution2;
      if (vsSolution2 == null || !ErrorHandler.Succeeded(vsSolution2.RemoveVirtualProject(this, (uint)__VSREMOVEVPFLAGS.REMOVEVP_DontSaveHierarchy)))
        return;
      _vsHierarchyActive = false;
    }

    private void Open() {
      if (!(this is IVsSelectionEvents))
        return;
      IVsMonitorSelection monitorSelection = this._serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
      if (monitorSelection == null)
        return;
      monitorSelection.AdviseSelectionEvents(this as IVsSelectionEvents, out this._selectionEventsCookie);
    }

    public int AdviseHierarchyEvents(IVsHierarchyEvents pEventSink, out uint pdwCookie) {
      pdwCookie = _eventSinks.Add(pEventSink);
      return 0;
    }

    public int Close() {
      _logger.LogHierarchy("Close");
      if ((int)_selectionEventsCookie != 0) {
        IVsMonitorSelection monitorSelection = this._serviceProvider.GetService(typeof(IVsMonitorSelection)) as IVsMonitorSelection;
        if (monitorSelection != null)
          monitorSelection.UnadviseSelectionEvents(this._selectionEventsCookie);
        _selectionEventsCookie = 0U;
      }
      return VSConstants.S_OK;
    }

    public int GetCanonicalName(uint itemid, out string pbstrName) {
      _logger.LogHierarchy("GetCanonicalName({0})", (int)itemid);
      pbstrName = null;

      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;

      pbstrName = node.Name;
      return VSConstants.S_OK;
    }

    public int GetGuidProperty(uint itemid, int propid, out Guid pguid) {
      _logger.LogPropertyGuid("GetGuidProperty", itemid, propid);
      pguid = new Guid(GuidList.GuidVsChromiumPkgString);
      return VSConstants.S_OK;
    }

    public int GetNestedHierarchy(uint itemid, ref Guid iidHierarchyNested, out IntPtr ppHierarchyNested, out uint pitemidNested) {
      _logger.LogHierarchy("GetNestedHierarchy({0})", (int)itemid);
      pitemidNested = uint.MaxValue;
      ppHierarchyNested = IntPtr.Zero;
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
          case (int)__VSHPROPID.VSHPROPID_IconImgList:
            pvar = (int)ImageListPtr;
            return 0;
          default:
            return node.GetProperty(propid, out pvar);
        }
      }

      pvar = null;
      return VSConstants.E_NOTIMPL;
    }

    public int GetSite(out Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
      _logger.LogHierarchy("GetSite()");
      site = _site;
      return 0;
    }

    public int ParseCanonicalName(string pszName, out uint pitemid) {
      _logger.LogHierarchy("ParseCanonicalName({0})", pszName);
      pitemid = 0U;
      return VSConstants.E_NOTIMPL;
    }

    public int QueryClose(out int pfCanClose) {
      pfCanClose = 1;
      return 0;
    }

    public int SetGuidProperty(uint itemid, int propid, ref Guid rguid) {
      _logger.LogProperty("SetGuidProperty - ", itemid, propid);
      return VSConstants.E_NOTIMPL;
    }

    public int SetProperty(uint itemid, int propid, object var) {
      _logger.LogProperty("SetProperty - ", itemid, propid);
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;
      switch (propid) {
        case (int)__VSHPROPID.VSHPROPID_ParentHierarchyItemid:
        case (int)__VSHPROPID.VSHPROPID_ParentHierarchy:
          return VSConstants.E_NOTIMPL;
        default:
          return node.SetProperty(propid, var);
      }
    }

    public int SetSite(Microsoft.VisualStudio.OLE.Interop.IServiceProvider site) {
      _site = site;
      return 0;
    }

    public int UnadviseHierarchyEvents(uint dwCookie) {
      _eventSinks.RemoveAt(dwCookie);
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

    public int QueryStatusCommand(uint itemid, ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText) {
      for (var index = 0; index < cCmds; ++index) {
        _logger.LogQueryStatusCommand(itemid, pguidCmdGroup, prgCmds[index].cmdID);

        NodeViewModel node;
        if (!_nodes.FindNode(itemid, out node)) {
          return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        var commandId = new CommandID(pguidCmdGroup, (int)prgCmds[index].cmdID);
        VsHierarchyCommandHandler handler;
        if (_commandHandlers.TryGetValue(commandId, out handler)) {
          if (handler.IsEnabled(node)) {
            prgCmds[index].cmdf = (uint)(OLECMDF.OLECMDF_SUPPORTED | OLECMDF.OLECMDF_ENABLED);
            return VSConstants.S_OK;
          }
        }
      }
      return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    public int ExecCommand(uint itemid, ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut) {
      _logger.LogExecCommand(itemid, pguidCmdGroup, nCmdID, nCmdexecopt);

      var commandId = new CommandID(pguidCmdGroup, (int)nCmdID);
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node)) {
        return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
      }

      VsHierarchyCommandHandler handler;
      if (_commandHandlers.TryGetValue(commandId, out handler)) {
        if (!handler.IsEnabled(node)) {
          return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
        }
        handler.Execute(new CommandArgs(commandId, node, pvaIn, pvaOut));
        return VSConstants.S_OK;
      }

      return (int)Constants.OLECMDERR_E_NOTSUPPORTED;
    }

    public int AddItemFromPackage(string pszItemName, VSADDRESULT[] pResult) {
      _logger.LogHierarchy("AddItemFromPackage({0})", pszItemName);
      return AddItem(uint.MaxValue, VSADDITEMOPERATION.VSADDITEMOP_OPENFILE, pszItemName, 0U, (string[])null, IntPtr.Zero, pResult);
    }

    public int AddItem(uint itemid, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, VSADDRESULT[] pResult) {
      _logger.LogHierarchy("AddItem({0})", (int)itemid);
      Guid guid = Guid.Empty;
      return AddItemWithSpecific(itemid, dwAddItemOperation, pszItemName, cFilesToOpen, rgpszFilesToOpen, hwndDlgOwner, 0U, ref guid, (string)null, ref guid, pResult);
    }

    public int AddItemWithSpecific(uint itemid, VSADDITEMOPERATION dwAddItemOperation, string pszItemName, uint cFilesToOpen, string[] rgpszFilesToOpen, IntPtr hwndDlgOwner, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, VSADDRESULT[] pResult) {
      _logger.LogHierarchy("AddItemWithSpecific({0})", (int)itemid);
      return VSConstants.E_NOTIMPL;
    }

    public int GenerateUniqueItemName(uint itemid, string pszExt, string pszSuggestedRoot, out string pbstrItemName) {
      _logger.LogHierarchy("GenerateUniqueItemName({0})", (int)itemid);
      pbstrItemName = null;
      return VSConstants.E_NOTIMPL;
    }

    public int GetItemContext(uint itemid, out Microsoft.VisualStudio.OLE.Interop.IServiceProvider ppSP) {
      _logger.LogHierarchy("GetItemContext({0})", (int)itemid);
      ppSP = null;
      return 0;
    }

    public int GetMkDocument(uint itemid, out string pbstrMkDocument) {
      _logger.LogHierarchy("GetMkDocument({0})", (int)itemid);
      pbstrMkDocument = null;
      NodeViewModel node;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;
      pbstrMkDocument = node.GetMkDocument();
      return 0;
    }

    public int IsDocumentInProject(string pszMkDocument, out int pfFound, VSDOCUMENTPRIORITY[] pdwPriority, out uint pitemid) {
      _logger.LogHierarchy("IsDocumentInProject({0})", pszMkDocument);
      pfFound = 0;
      pitemid = 0U;
      if (pdwPriority != null && pdwPriority.Length >= 1)
        pdwPriority[0] = VSDOCUMENTPRIORITY.DP_Unsupported;
      return 0;
    }

    public int OpenItem(uint itemid, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      _logger.LogHierarchy("OpenItem({0})", (int)itemid);
      ppWindowFrame = null;

      NodeViewModel node = null;
      uint flags = 536936448U;
      int hresult = 0;
      if (!_nodes.FindNode(itemid, out node))
        return VSConstants.E_FAIL;

      if (string.IsNullOrEmpty(node.Path))
        return VSConstants.E_NOTIMPL;
      IVsUIHierarchy hierarchy;
      int isDocInProj;
      uint itemid1;

      if (!VsShellUtilities.IsDocumentOpen(_serviceProvider, node.Path, rguidLogicalView, out hierarchy, out itemid1, out ppWindowFrame)) {
        IVsHierarchy hierOpen = null;
        IsDocumentInAnotherProject(node.Path, out hierOpen, out itemid1, out isDocInProj);
        if (hierOpen == null) {
          hresult = OpenItemViaMiscellaneousProject(flags, node.Path, ref rguidLogicalView, out ppWindowFrame);
        } else {
          var vsProject3 = hierOpen as IVsProject3;
          hresult = vsProject3 == null
            ? OpenItemViaMiscellaneousProject(flags, node.Path, ref rguidLogicalView, out ppWindowFrame)
            : vsProject3.OpenItem(itemid1, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
        }
      }
      if (ppWindowFrame != null)
        hresult = ppWindowFrame.Show();
      return hresult;
    }

    public int OpenItemWithSpecific(uint itemid, uint grfEditorFlags, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      _logger.LogHierarchy("OpenItemWithSpecific({0})", (int)itemid);
      return OpenItem(itemid, ref rguidLogicalView, punkDocDataExisting, out ppWindowFrame);
    }

    public int RemoveItem(uint dwReserved, uint itemid, out int pfResult) {
      pfResult = 0;
      return VSConstants.E_NOTIMPL;
    }

    public int ReopenItem(uint itemid, ref Guid rguidEditorType, string pszPhysicalView, ref Guid rguidLogicalView, IntPtr punkDocDataExisting, out IVsWindowFrame ppWindowFrame) {
      ppWindowFrame = null;
      return VSConstants.E_NOTIMPL;
    }

    public int TransferItem(string pszMkDocumentOld, string pszMkDocumentNew, IVsWindowFrame punkWindowFrame) {
      return VSConstants.S_OK;
    }

    private int OpenItemViaMiscellaneousProject(uint flags, string moniker, ref Guid rguidLogicalView, out IVsWindowFrame ppWindowFrame) {
      ppWindowFrame = null;

      var miscellaneousProject = VsShellUtilities.GetMiscellaneousProject(this._serviceProvider);
      int hresult = VSConstants.E_FAIL;
      if (miscellaneousProject != null &&
         _serviceProvider.GetService(typeof(SVsUIShellOpenDocument)) is IVsUIShellOpenDocument) {
        var externalFilesManager = this._serviceProvider.GetService(typeof(SVsExternalFilesManager)) as IVsExternalFilesManager;
        externalFilesManager.TransferDocument(null, moniker, null);
        IVsProject ppProject;
        hresult = externalFilesManager.GetExternalFilesProject(out ppProject);
        if (ppProject != null) {
          int pfFound;
          uint pitemid;
          var pdwPriority = new VSDOCUMENTPRIORITY[1];
          hresult = ppProject.IsDocumentInProject(moniker, out pfFound, pdwPriority, out pitemid);
          if (pfFound == 1 && (int)pitemid != -1)
            hresult = ppProject.OpenItem(pitemid, ref rguidLogicalView, IntPtr.Zero, out ppWindowFrame);
        }
      }
      return hresult;
    }

    private void IsDocumentInAnotherProject(string originalPath, out IVsHierarchy hierOpen, out uint itemId, out int isDocInProj) {
      var vsSolution = _serviceProvider.GetService(typeof(SVsSolution)) as IVsSolution;
      var rguidEnumOnlyThisType = Guid.Empty;
      IEnumHierarchies ppenum;
      itemId = uint.MaxValue;
      hierOpen = null;
      isDocInProj = 0;
      vsSolution.GetProjectEnum(1U, ref rguidEnumOnlyThisType, out ppenum);
      if (ppenum == null)
        return;

      ppenum.Reset();
      uint pceltFetched = 1U;
      var rgelt = new IVsHierarchy[1];
      ppenum.Next(1U, rgelt, out pceltFetched);
      while ((int)pceltFetched == 1) {
        var vsProject = rgelt[0] as IVsProject;
        var pdwPriority = new VSDOCUMENTPRIORITY[1];
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
  }
}
