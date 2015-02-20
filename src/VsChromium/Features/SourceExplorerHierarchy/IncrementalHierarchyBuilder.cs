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
        foreach (var childEntry in directoryEntry.Entries) {
          CreateNodeViewModel(childEntry, newParent);
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

      foreach (var oldChild in diffs.LeftOnlyItems) {
        _changes.DeletedItems.Add(oldChild.ItemId);
      }

      foreach (var newChild in diffs.RightOnlyItems) {
        newChild.ItemId = _newNodeNextItemId;
        _newNodeNextItemId++;
        newChild.IsExpanded = newParent.IsRoot;
        _newNodes.AddNode(newChild);

        if (oldParent != null) {
          _changes.AddedItems.Add(newChild.ItemId);
        }
      }

      foreach (var pair in diffs.CommonItems) {
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
          // TODO(rpaquay): Perf?
          var oldChildNode = diffs.CommonItems
              .Where(x => x.RigthtItem == newChildNode)
              .Select(x => x.LeftItem)
              .FirstOrDefault();

          AddNodeForChildren(childEntry, oldChildNode, newChildNode);
        }
      }
    }

    private NodeViewModel CreateNodeViewModel(FileSystemEntry entry, NodeViewModel parent) {
      Debug.Assert(entry != null);
      Debug.Assert(parent != null);

      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel()
        : (NodeViewModel)new FileNodeViewModel();

      var path = parent.IsRoot
        ? entry.Name
        : PathHelpers.CombinePaths(parent.Path, entry.Name);

      node.Caption = entry.Name;
      node.Name = entry.Name;
      node.Path = path;
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
      parent.AddChild(node);
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