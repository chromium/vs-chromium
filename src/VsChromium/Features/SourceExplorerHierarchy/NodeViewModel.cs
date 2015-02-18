// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Drawing;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using VsChromium.Core.Files;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class NodeViewModel {
    private const int NoImage = -1;
    private readonly List<NodeViewModel> _children = new List<NodeViewModel>();
    private readonly Dictionary<int, object> _properties = new Dictionary<int, object>();
    private NodeViewModel _parent;

    public NodeViewModel() {
      OpenFolderImageIndex = NoImage;
      ImageIndex = NoImage;
      DocCookie = uint.MaxValue;
      ItemId = uint.MaxValue;
    }

    public uint ItemId { get; set; }
    public string Name { get; set; }
    public string Caption { get; set; }
    public string Path { get; set; }
    public uint DocCookie { get; set; }
    public int ImageIndex { get; set; }
    public int OpenFolderImageIndex { get; set; }
    public Icon Icon { get; set; }
    public Icon OpenFolderIcon { get; set; }
    public bool IsExpanded { get; set; }
    public bool ExpandByDefault { get; set; }

    public bool IsRoot {
      get {
        return ItemId == VsHierarchyNodes.RootNodeItemId;
      }
    }

    public uint GetFirstChildItemId() {
      if (_children.Count == 0)
        return uint.MaxValue;

      return _children[0].ItemId;
    }

    private uint GetParentItemId() {
      if (_parent == null)
        return uint.MaxValue;

      return _parent.ItemId;
    }

    private uint GetNextSiblingItemId() {
      // TODO(rpaquay): Perf?
      if (_parent == null)
        return uint.MaxValue;

      var index = _parent._children.IndexOf(this);
      if (index < 0 || index >= _parent._children.Count - 1)
        return uint.MaxValue;
      return _parent._children[index + 1].ItemId;
    }

    public void AddChild(NodeViewModel node) {
      node._parent = this;
      _children.Add(node);
    }

    public void RemoveChildren() {
      _children.Clear();
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
      return _children.Count;
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

      if (SystemPathComparer.Instance.StringComparer.Equals(parentNode.Path, searchMoniker)) {
        foundNode = parentNode;
        return true;
      }

      foreach (var child in parentNode._children) {
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
      return Path;
    }

    public int SetProperty(int propid, object var) {
      if (propid == (int)__VSHPROPID.VSHPROPID_Expanded)
        IsExpanded = (bool)var;
      else
        _properties[propid] = var;
      return VSConstants.S_OK;
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
          pvar = (object)this.ItemId;
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
            pvar = iconHandle;
            break;
          }
        case (int)__VSHPROPID.VSHPROPID_IconHandle: {
            var iconHandle = GetIconHandle();
            if (iconHandle == IntPtr.Zero)
              return VSConstants.E_NOTIMPL;
            pvar = iconHandle;
            break;
          }
        case (int)__VSHPROPID.VSHPROPID_ProjectName:
        case (int)__VSHPROPID.VSHPROPID_SaveName:
          pvar = (object)this.Name;
          break;
        case (int)__VSHPROPID.VSHPROPID_ExpandByDefault:
          pvar = (object)(this.ExpandByDefault ? 1 : 0);
          break;
        case (int)__VSHPROPID.VSHPROPID_Expandable:
          pvar = this._children.Count > 0;
          break;
        case (int)__VSHPROPID.VSHPROPID_IconIndex: {
            int imageIndex = this.GetImageIndex();
            if (imageIndex == NoImage)
              return VSConstants.E_NOTIMPL;
            pvar = (object)imageIndex;
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

  public class DirectoryNodeViewModel : NodeViewModel {
    
  }

  public class FileNodeViewModel : NodeViewModel {
    
  }
}
