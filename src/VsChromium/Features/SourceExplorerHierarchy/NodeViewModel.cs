// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public abstract class NodeViewModel {
    public const int NoImage = -1;
    private NodeViewModel _parent;

    protected NodeViewModel() {
      DocCookie = uint.MaxValue;
      ChildIndex = -1; // No set
      ItemId = VSConstants.VSITEMID_NIL;
      Template = NodeViewModelTemplate.Default;
    }

    public uint ItemId { get; set; }
    /// <summary>Index in list of children of parent element</summary>
    public int ChildIndex { get; set; }
    public string Name { get; set; }
    public string Caption { get; set; }
    public uint DocCookie { get; set; }
    public bool IsExpanded { get; set; }
    public NodeViewModelTemplate Template { get; set; }

    public int ImageIndex {
      get { return Template.ImageIndex; }
    }
    public int OpenFolderImageIndex {
      get { return Template.OpenFolderImageIndex; }
    }
    public Icon Icon {
      get { return Template.Icon; }
    }
    public Icon OpenFolderIcon {
      get { return Template.OpenFolderIcon; }
    }
    public bool ExpandByDefault {
      get { return Template.ExpandByDefault; }
    }

    public bool IsRoot {
      get {
        return ItemId == VsHierarchyNodes.RootNodeItemId;
      }
    }

    protected abstract IList<NodeViewModel> ChildrenImpl { get; }

    public IList<NodeViewModel> Children {
      get { return ChildrenImpl; }
    }

    public NodeViewModel Parent {
      get { return _parent; }
    }

    public string FullPath {
      get {
        var sb = new StringBuilder(260);
        AppendFullPath(sb);
        return sb.ToString();
      }
    }

    public string RelativePath {
      get {
        var sb = new StringBuilder(260);
        AppendRelativePath(sb);
        return sb.ToString();
      }
    }

    private void AppendFullPath(StringBuilder sb) {
      if (string.IsNullOrEmpty(this.Name))
        return;

      if (PathHelpers.IsAbsolutePath(this.Name)) {
        sb.Append(this.Name);
        return;
      }

      if (_parent == null)
        return;

      _parent.AppendFullPath(sb);
      if (sb.Length > 0) {
        sb.Append(Path.DirectorySeparatorChar);
      }
      sb.Append(this.Name);
    }

    private void AppendRelativePath(StringBuilder sb) {
      if (string.IsNullOrEmpty(this.Name))
        return;

      if (PathHelpers.IsAbsolutePath(this.Name))
        return;

      if (_parent == null)
        return;

      _parent.AppendRelativePath(sb);
      if (sb.Length > 0) {
        sb.Append(Path.DirectorySeparatorChar);
      }
      sb.Append(this.Name);
    }

    public uint GetFirstChildItemId() {
      if (ChildrenImpl.Count == 0)
        return VSConstants.VSITEMID_NIL;

      return ChildrenImpl[0].ItemId;
    }

    private uint GetParentItemId() {
      if (_parent == null)
        return VSConstants.VSITEMID_NIL;

      return _parent.ItemId;
    }

    public uint GetNextSiblingItemId() {
      if (_parent == null)
        return VSConstants.VSITEMID_NIL;

      var index = ChildIndex;
      Invariants.Assert(0 <= index && index < _parent.ChildrenImpl.Count);
      if (index < 0 || index >= _parent.ChildrenImpl.Count - 1)
        return VSConstants.VSITEMID_NIL;
      return _parent.ChildrenImpl[index + 1].ItemId;
    }

    public uint GetPreviousSiblingItemId() {
      if (_parent == null)
        return VSConstants.VSITEMID_NIL;

      var index = ChildIndex;
      Invariants.Assert(0 <= index && index < _parent.ChildrenImpl.Count);
      if (index < 1)
        return VSConstants.VSITEMID_NIL;
      return _parent.ChildrenImpl[index - 1].ItemId;
    }

    public void AddChild(NodeViewModel node) {
      node._parent = this;
      node.ChildIndex = ChildrenImpl.Count;
      ChildrenImpl.Add(node);
    }

    private IntPtr GetIconHandleForImageIndex(int imageIndex) {
      return IntPtr.Zero;
    }

    private IntPtr GetIconHandle() {
      var icon = this.Icon;
      if (icon != null)
        return icon.Handle;
      return GetIconHandleForImageIndex(GetImageIndex());
    }

    private IntPtr GetOpenFolderIconHandle() {
      var icon = OpenFolderIcon;
      if (Icon != null && icon != null)
        return icon.Handle;
      return GetIconHandleForImageIndex(GetOpenFolderImageIndex());
    }

    public int GetChildrenCount() {
      return ChildrenImpl.Count;
    }

    private int GetImageIndex() {
      return ImageIndex;
    }

    private int GetOpenFolderImageIndex() {
      return OpenFolderImageIndex;
    }

    private bool FindNodeByMonikerHelper(NodeViewModel parentNode, string searchMoniker, out NodeViewModel foundNode) {
      foundNode = null;
      if (parentNode == null)
        return false;

      if (SystemPathComparer.Instance.StringComparer.Equals(parentNode.FullPath, searchMoniker)) {
        foundNode = parentNode;
        return true;
      }

      foreach (var child in parentNode.ChildrenImpl) {
        if (FindNodeByMonikerHelper(child, searchMoniker, out foundNode)) {
          return true;
        }
      }
      return false;
    }

    public bool FindNodeByMoniker(string searchMoniker, out NodeViewModel node) {
      node = null;
      if (!IsRoot)
        return false;
      return FindNodeByMonikerHelper(this, searchMoniker, out node);
    }

    public string GetMkDocument() {
      return FullPath;
    }

    public int SetProperty(int propid, object var) {
      if (propid == (int)__VSHPROPID.VSHPROPID_Expanded) {
        IsExpanded = (bool)var;
        return VSConstants.S_OK;
      }

      return VSConstants.E_NOTIMPL;
    }

    public int GetProperty(int propid, out object pvar) {
      pvar = null;
      switch (propid) {
        case (int)__VSHPROPID2.VSHPROPID_KeepAliveDocument:
          pvar = true;
          break;
        case (int)__VSHPROPID.VSHPROPID_OverlayIconIndex:
          pvar = VSOVERLAYICON.OVERLAYICON_NONE;
          break;
        case (int)__VSHPROPID.VSHPROPID_NextVisibleSibling:
        case (int)__VSHPROPID.VSHPROPID_NextSibling:
          pvar = GetNextSiblingItemId();
          break;
        case (int)__VSHPROPID.VSHPROPID_FirstVisibleChild:
        case (int)__VSHPROPID.VSHPROPID_FirstChild:
          pvar = GetFirstChildItemId();
          break;
        case (int)__VSHPROPID.VSHPROPID_Expanded:
          pvar = (this.IsExpanded ? 1 : 0);
          break;
        case (int)__VSHPROPID.VSHPROPID_ItemDocCookie:
          pvar = ItemId;
          break;
        case (int)__VSHPROPID.VSHPROPID_OpenFolderIconIndex: {
            var iconIndex = GetOpenFolderImageIndex();
            if (iconIndex == NoImage)
              iconIndex = GetImageIndex();
            if (iconIndex == NoImage)
              return VSConstants.E_NOTIMPL;
            pvar = iconIndex;
            break;
          }
        case (int)__VSHPROPID.VSHPROPID_OpenFolderIconHandle: {
            var iconHandle = GetOpenFolderIconHandle();
            if (iconHandle == IntPtr.Zero)
              iconHandle = GetIconHandle();
            if (iconHandle == IntPtr.Zero)
              return VSConstants.E_NOTIMPL;
            pvar = (int)iconHandle;
            break;
          }
        case (int)__VSHPROPID.VSHPROPID_IconHandle: {
            var iconHandle = GetIconHandle();
            if (iconHandle == IntPtr.Zero)
              return VSConstants.E_NOTIMPL;
            pvar = (int)iconHandle;
            break;
          }
        // BSTR. Name for project (VSITEMID_ROOT) or item.
        case (int)__VSHPROPID.VSHPROPID_Name:
        // File name specified on the File Save menu.
        case (int)__VSHPROPID.VSHPROPID_SaveName:
          pvar = Name;
          break;
        case (int)__VSHPROPID.VSHPROPID_ExpandByDefault:
          pvar = (ExpandByDefault ? 1 : 0);
          break;
        case (int)__VSHPROPID.VSHPROPID_Expandable:
          pvar = ChildrenImpl.Count > 0;
          break;
        case (int)__VSHPROPID.VSHPROPID_IconIndex: {
            int imageIndex = GetImageIndex();
            if (imageIndex == NoImage)
              return VSConstants.E_NOTIMPL;
            pvar = imageIndex;
            break;
          }
        case (int)__VSHPROPID.VSHPROPID_Caption:
          pvar = Caption;
          break;
        case (int)__VSHPROPID.VSHPROPID_Parent:
          pvar = GetParentItemId();
          break;
        default:
          return VSConstants.E_NOTIMPL;
      }
      return VSConstants.S_OK;
    }
  }
}
