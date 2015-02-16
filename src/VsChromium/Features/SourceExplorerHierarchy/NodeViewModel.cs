// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Files;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeViewModel {
    private const int NoImage = -1;
    private readonly List<NodeViewModel> _children = new List<NodeViewModel>();
    private readonly Dictionary<int, object> _properties = new Dictionary<int, object>();
    private readonly VsHierarchy _owningHierarchy;
    private NodeViewModel _prevSibling;
    private NodeViewModel _parent;
    private NodeViewModel _firstChild;
    private NodeViewModel _lastChild;

    public NodeViewModel(VsHierarchy hier) {
      OpenFolderImageIndex = -1;
      ImageIndex = -1;
      DocCookie = uint.MaxValue;
      ItemId = uint.MaxValue;
      this._owningHierarchy = hier;
    }

    public uint ItemId { get; set; }
    public string Name { get; set; }
    public string Caption { get; set; }
    public string Moniker { get; set; }
    public string LocalMoniker { get; set; }
    public uint DocCookie { get; set; }
    public NodeViewModel NextSibling { get; set; }
    public int ImageIndex { get; set; }
    public int OpenFolderImageIndex { get; set; }
    public Icon Icon { get; set; }
    public Icon OpenFolderIcon { get; set; }
    public bool IsExpanded { get; set; }
    public bool ExpandByDefault { get; set; }

    public IEnumerable<NodeViewModel> Children {
      get {
        return _children;
      }
    }

    public bool IsRoot {
      get {
        return (int)this.ItemId == -2;
      }
    }

    public string OriginalPath {
      get {
        if (this.IsRemote())
          return this.Moniker;
        return this.LocalMoniker;
      }
    }

    public uint GetFirstChildItemId() {
      if (this._firstChild != null)
        return this._firstChild.ItemId;
      return uint.MaxValue;
    }

    private uint GetParentItemId() {
      if (this._parent != null)
        return this._parent.ItemId;
      return uint.MaxValue;
    }

    private uint GetNextSiblingItemId() {
      if (this.NextSibling != null)
        return this.NextSibling.ItemId;
      return uint.MaxValue;
    }

    private void AddSiblingAfter(NodeViewModel node) {
      if (this.NextSibling != null) {
        this.NextSibling._prevSibling = node;
        node.NextSibling = this.NextSibling;
      }
      node._prevSibling = this;
      this.NextSibling = node;
    }

    public void AddChild(NodeViewModel node) {
      node._parent = this;
      if (this._lastChild == null)
        this._firstChild = node;
      else
        this._lastChild.AddSiblingAfter(node);
      this._lastChild = node;
      this._children.Add(node);
    }

    public void RemoveChildren() {
      var list = this._children.ToList();
      foreach (var virtualProjectNode in list)
        this._children.Remove(virtualProjectNode);
      this._firstChild = null;
      this._lastChild = null;
    }

    private IntPtr GetIconHandleForImageIndex(int imageIndex) {
      return IntPtr.Zero;
    }

    private IntPtr GetIconHandle() {
      Icon icon = this.Icon;
      if (this.Icon != null)
        return icon.Handle;
      return this.GetIconHandleForImageIndex(this.GetImageIndex());
    }

    private IntPtr GetOpenFolderIconHandle() {
      Icon icon = this.OpenFolderIcon;
      if (this.Icon != null && icon != null)
        return icon.Handle;
      return this.GetIconHandleForImageIndex(this.GetOpenFolderImageIndex());
    }

    public int GetChildrenCount() {
      return this._children.Count;
    }

    private int GetImageIndex() {
      return this.ImageIndex;
    }

    private int GetOpenFolderImageIndex() {
      return this.OpenFolderImageIndex;
    }

    public void Inactivate() {
      this.Caption = this.Name;
    }

    public void Activate() {
      this.Caption = string.Format("{0} active", this.Name);
    }

    private bool FindNodeByMonikerHelper(NodeViewModel parentNode, string searchMoniker, out NodeViewModel foundNode) {
      foundNode = (NodeViewModel)null;
      if (parentNode == null)
        return false;
      if (SystemPathComparer.Instance.StringComparer.Equals(parentNode.LocalMoniker, searchMoniker)) {
        foundNode = parentNode;
        return true;
      }
      bool flag = false;
      for (NodeViewModel parentNode1 = parentNode._firstChild; parentNode1 != null; parentNode1 = parentNode1.NextSibling) {
        flag = this.FindNodeByMonikerHelper(parentNode1, searchMoniker, out foundNode);
        if (flag)
          break;
      }
      return flag;
    }

    public bool FindNodeByMoniker(string searchMoniker, out NodeViewModel node) {
      node = (NodeViewModel)null;
      if (!this.IsRoot)
        return false;
      return this.FindNodeByMonikerHelper(this, searchMoniker, out node);
    }

    public void Redraw() {
      foreach (IVsHierarchyEvents vsHierarchyEvents in (IEnumerable)this._owningHierarchy.EventSinks) {
        vsHierarchyEvents.OnPropertyChanged(this.ItemId, (int)__VSHPROPID.VSHPROPID_Caption, 0U);
        vsHierarchyEvents.OnPropertyChanged(this.ItemId, (int)__VSHPROPID.VSHPROPID_IconIndex, 0U);
        vsHierarchyEvents.OnPropertyChanged(this.ItemId, (int)__VSHPROPID.VSHPROPID_StateIconIndex, 0U);
      }
    }

    public bool IsRemote() {
      return !string.IsNullOrEmpty(this.Moniker);
    }

    public string GetMkDocument() {
      return this.LocalMoniker;
    }

    public int SetProperty(int propid, object var) {
      if (propid == -2035)
        this.IsExpanded = (bool)var;
      else
        this._properties[propid] = var;
      return 0;
    }

    public int GetProperty(int propid, out object pvar) {
      pvar = null;
      switch (propid) {
        case (int)__VSHPROPID2.VSHPROPID_KeepAliveDocument:
          pvar = (object)true;
          break;
        case (int)__VSHPROPID.VSHPROPID_OverlayIconIndex:
          pvar = (object)VSOVERLAYICON.OVERLAYICON_NONE;
          break;
        case (int)__VSHPROPID.VSHPROPID_NextVisibleSibling:
        case (int)__VSHPROPID.VSHPROPID_NextSibling:
          pvar = (object)this.GetNextSiblingItemId();
          break;
        case (int)__VSHPROPID.VSHPROPID_FirstVisibleChild:
        case (int)__VSHPROPID.VSHPROPID_FirstChild:
          pvar = (object)this.GetFirstChildItemId();
          break;
        case (int)__VSHPROPID.VSHPROPID_Expanded:
          pvar = (object)(this.IsExpanded ? 1 : 0);
          break;
        case (int)__VSHPROPID.VSHPROPID_ItemDocCookie:
          pvar = (object)this.ItemId;
          break;
        case (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex:
          int num1 = this.GetOpenFolderImageIndex();
          if (num1 == -1)
            num1 = this.GetImageIndex();
          if (num1 == -1)
            return VSConstants.E_NOTIMPL;
          pvar = (object)num1;
          break;
        case (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle:
          IntPtr num2 = this.GetOpenFolderIconHandle();
          if (num2 == IntPtr.Zero)
            num2 = this.GetIconHandle();
          if (num2 == IntPtr.Zero)
            return VSConstants.E_NOTIMPL;
          pvar = (object)num2;
          break;
        case (int)__VSHPROPID.VSHPROPID_IconHandle:
          IntPtr iconHandle = this.GetIconHandle();
          if (iconHandle == IntPtr.Zero)
            return VSConstants.E_NOTIMPL;
          pvar = (object)iconHandle;
          break;
        case (int)__VSHPROPID.VSHPROPID_ProjectName:
        case (int)__VSHPROPID.VSHPROPID_SaveName:
          pvar = (object)this.Name;
          break;
        case (int)__VSHPROPID.VSHPROPID_ExpandByDefault:
          pvar = (object)(this.ExpandByDefault ? 1 : 0);
          break;
        case (int)__VSHPROPID.VSHPROPID_Expandable:
          pvar = this._firstChild == null ? (object)false : (object)true;
          break;
        case (int)__VSHPROPID.VSHPROPID_IconIndex:
          int imageIndex = this.GetImageIndex();
          if (imageIndex == -1)
            return VSConstants.E_NOTIMPL;
          pvar = (object)imageIndex;
          break;
        case (int)__VSHPROPID.VSHPROPID_Caption :
          pvar = Caption;
          break;
        case (int)__VSHPROPID.VSHPROPID_Parent:
          pvar = GetParentItemId();
          break;
        default:
          return VSConstants.E_NOTIMPL;
      }
      return 0;
    }
  }
}