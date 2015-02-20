// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.Language.Intellisense;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Utility;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalHierarchyBuilder {
    private readonly IVsGlyphService _vsGlyphService;
    private readonly VsHierarchyNodes _oldNodes;
    private readonly FileSystemTree _fileSystemTree;
    private readonly VsHierarchyNodes _newNodes = new VsHierarchyNodes();
    private readonly VsHierarchyChanges _changes = new VsHierarchyChanges();
    private uint _newNodeNextItemId;

    public IncrementalHierarchyBuilder(
      IVsGlyphService vsGlyphService,
      VsHierarchyNodes oldNodes,
      FileSystemTree fileSystemTree) {
      _vsGlyphService = vsGlyphService;
      _oldNodes = oldNodes;
      _fileSystemTree = fileSystemTree;
    }

    public IncrementalBuildResult Run() {
      using (new TimeElapsedLogger("Computing NodesViewModel with diffs")) {
        _newNodeNextItemId = _oldNodes.MaxItemId + 1;

        SetupRootNode(_newNodes.RootNode);

        if (_fileSystemTree != null) {
          AddNodeForChildren(_fileSystemTree.Root, _oldNodes.RootNode, _newNodes.RootNode);
        }

        return new IncrementalBuildResult {
          OldNodes = _oldNodes,
          NewNodes = _newNodes,
          Changes = _changes,
        };
      }
    }

    private void AddNodeForChildren(FileSystemEntry entry, NodeViewModel oldParent, NodeViewModel newParent) {
      Debug.Assert(entry != null);
      Debug.Assert(newParent != null);
      Debug.Assert(newParent.Children.Count == 0);

      // Create children nodes
      var directoryEntry = entry as DirectoryEntry;
      if (directoryEntry != null) {
        // PERF: Avoid memory allocation
        for (var i = 0; i < directoryEntry.Entries.Count; i++) {
          var child = CreateNodeViewModel(directoryEntry.Entries[i], newParent);
          newParent.AddChild(child);
        }
      }

      // Note: It is correct to compare the "Name" property only for computing
      // diffs, as we are guaranteed that both nodes have the same parent, hence
      // are located in the same directory. We also use the
      // System.Reflection.Type to handle the fact a directory can be deleted
      // and then a name with the same name can be added. We need to consider
      // that as a pair of "delete/add" instead of a "no-op".
      var diffs = ArrayUtilities.BuildArrayDiffs(
        oldParent == null ? ArrayUtilities.EmptyList<NodeViewModel>.Instance : oldParent.Children,
        newParent.Children,
        NodeTypeAndNameComparer.Instance);

      // PERF: Avoid memory allocation
      for (var i = 0; i < diffs.LeftOnlyItems.Count; i++) {
        _changes.DeletedItems.Add(diffs.LeftOnlyItems[i].ItemId);
      }

      // PERF: Avoid memory allocation
      for (var i = 0; i < diffs.RightOnlyItems.Count; i++) {
        var newChild = diffs.RightOnlyItems[i];
        newChild.ItemId = _newNodeNextItemId;
        _newNodeNextItemId++;
        newChild.IsExpanded = newParent.IsRoot;
        _newNodes.AddNode(newChild);

        if (oldParent != null) {
          _changes.AddedItems.Add(newChild.ItemId);
        }
      }

      // PERF: Avoid memory allocation
      for (var i = 0; i < diffs.CommonItems.Count; i++) {
        var pair = diffs.CommonItems[i];
        pair.RigthtItem.ItemId = pair.LeftItem.ItemId;
        pair.RigthtItem.IsExpanded = pair.LeftItem.IsExpanded;
        _newNodes.AddNode(pair.RigthtItem);
      }

      // Call recursively on all children
      if (directoryEntry != null) {
        Debug.Assert(directoryEntry.Entries.Count == newParent.Children.Count);
        for (var i = 0; i < newParent.Children.Count; i++) {
          var childEntry = directoryEntry.Entries[i];
          var newChildNode = newParent.Children[i];
          var oldChildNode = GetCommonOldNode(newParent, i, diffs, newChildNode);

          AddNodeForChildren(childEntry, oldChildNode, newChildNode);
        }
      }
    }

    private static NodeViewModel GetCommonOldNode(NodeViewModel newParent, int index, ArrayDiffsResult<NodeViewModel> diffs, NodeViewModel newChildNode) {
      if (diffs.CommonItems.Count == newParent.Children.Count) {
        return diffs.CommonItems[index].LeftItem;
      }

      for (var i = 0; i < diffs.CommonItems.Count; i++) {
        var pair = diffs.CommonItems[i];
        if (pair.RigthtItem == newChildNode)
          return pair.LeftItem;
      }

      return null;
    }

    private NodeViewModel CreateNodeViewModel(FileSystemEntry entry, NodeViewModel parent) {
      Debug.Assert(entry != null);
      Debug.Assert(parent != null);

      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel()
        : (NodeViewModel)new FileNodeViewModel();

      node.Caption = entry.Name;
      node.Name = entry.Name;
      node.ExpandByDefault = parent.IsRoot;
      if (directoryEntry != null) {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphClosedFolder,
          StandardGlyphItem.GlyphItemPublic);
        node.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphOpenFolder,
          StandardGlyphItem.GlyphItemPublic);
      } else {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphCSharpFile,
          StandardGlyphItem.GlyphItemPublic);
      }
      return node;
    }

    private class NodeTypeAndNameComparer : IEqualityComparer<NodeViewModel> {
      public static readonly NodeTypeAndNameComparer Instance = new NodeTypeAndNameComparer();

      public bool Equals(NodeViewModel x, NodeViewModel y) {
        if (x.GetType() != y.GetType())
          return false;

        return StringComparer.Ordinal.Equals(x.Name, y.Name);
      }

      public int GetHashCode(NodeViewModel obj) {
        return StringComparer.Ordinal.GetHashCode(obj.Name);
      }
    }

    private void SetupRootNode(NodeViewModel root) {
      var name = "VS Chromium Projects";
      root.Name = name;
      root.Caption = name;
      root.ExpandByDefault = true;
      root.ImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphClosedFolder,
        StandardGlyphItem.GlyphItemPublic);
      root.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
        StandardGlyphGroup.GlyphOpenFolder,
        StandardGlyphItem.GlyphItemPublic);
    }
  }
}