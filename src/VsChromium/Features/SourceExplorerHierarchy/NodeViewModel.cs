// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public abstract class NodeViewModel {
    public const int NoImage = -1;
    private readonly NodeViewModel _parent;

    protected NodeViewModel(NodeViewModel parent) {
      _parent = parent;
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

    public int ImageIndex => Template.ImageIndex;
    public int OpenFolderImageIndex => Template.OpenFolderImageIndex;
    public Icon Icon => Template.Icon;
    public Icon OpenFolderIcon => Template.OpenFolderIcon;
    public bool ExpandByDefault => Template.ExpandByDefault;
    public bool IsRoot => ItemId == VsHierarchyNodes.RootNodeItemId;
    public NodeViewModel Parent => _parent;
    public bool ChildrenLoaded { get; set; }
    public IList<NodeViewModel> Children => ChildrenImpl;
    public FullPath FullPath => new FullPath(FullPathString);

    public string FullPathString {
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
      AddChildImpl(node);
    }

    protected abstract void AddChildImpl(NodeViewModel node);

    public int GetChildrenCount() {
      return ChildrenImpl.Count;
    }

    public string GetMkDocument() {
      return FullPathString;
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
          pvar = (IsExpanded ? 1 : 0);
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

    protected abstract IList<NodeViewModel> ChildrenImpl { get; }

    private void AppendFullPath(StringBuilder sb) {
      if (string.IsNullOrEmpty(Name))
        return;

      if (PathHelpers.IsAbsolutePath(Name)) {
        sb.Append(Name);
        return;
      }

      if (_parent == null)
        return;

      _parent.AppendFullPath(sb);
      if (sb.Length > 0) {
        sb.Append(System.IO.Path.DirectorySeparatorChar);
      }
      sb.Append(Name);
    }

    private void AppendRelativePath(StringBuilder sb) {
      if (string.IsNullOrEmpty(Name))
        return;

      if (PathHelpers.IsAbsolutePath(Name))
        return;

      if (_parent == null)
        return;

      _parent.AppendRelativePath(sb);
      if (sb.Length > 0) {
        sb.Append(System.IO.Path.DirectorySeparatorChar);
      }
      sb.Append(Name);
    }

    private IntPtr GetIconHandleForImageIndex() {
      return IntPtr.Zero;
    }

    private IntPtr GetIconHandle() {
      var icon = Icon;
      if (icon != null)
        return icon.Handle;
      return GetIconHandleForImageIndex();
    }

    private IntPtr GetOpenFolderIconHandle() {
      var icon = OpenFolderIcon;
      if (Icon != null && icon != null)
        return icon.Handle;
      return GetIconHandleForImageIndex();
    }

    private int GetImageIndex() {
      return ImageIndex;
    }

    private int GetOpenFolderImageIndex() {
      return OpenFolderImageIndex;
    }
  }
}
