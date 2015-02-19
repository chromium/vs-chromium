// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.Language.Intellisense;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalHierarchyBuilder {
    private readonly IVsGlyphService _vsGlyphService;
    private readonly VsHierarchyNodes _oldNodes;
    private readonly FileSystemTree _fileSystemTree;
    private readonly VsHierarchyNodes _newNodes = new VsHierarchyNodes();
    private readonly Dictionary<string, NodeViewModel> _pathToNode = new Dictionary<string, NodeViewModel>(); 
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
      _newNodeNextItemId = _oldNodes.MaxItemId + 1;
      BuildPathToNodeMap(_oldNodes.RootNode);

      SetupRootNode(_newNodes.RootNode);

      if (_fileSystemTree != null) {
        foreach (var root in _fileSystemTree.Root.Entries) {
          AddRootEntry(root);
        }
      }

      return new IncrementalBuildResult {
        OldNodes = _oldNodes,
        NewNodes = _newNodes,
        Changes = BuildChanges()
      };
    }

    private VsHierarchyChanges BuildChanges() {
      var result = new VsHierarchyChanges();
      BuildChanges(_oldNodes.RootNode, _newNodes.RootNode, result);
      return result;
    }

    private void BuildChanges(NodeViewModel oldNode, NodeViewModel newNode, VsHierarchyChanges result) {
      var diff = ArrayUtilities.BuildArrayDiffs(oldNode.Children, newNode.Children, NodePathComparer.Instance);
      foreach (var left in diff.LeftOnlyItems) {
        result.DeletedItems.Add(left.ItemId);
      }
      foreach (var right in diff.RightOnlyItems) {
        result.AddedItems.Add(right.ItemId);
      }
      foreach (var leftRight in diff.CommonItems) {
        BuildChanges(leftRight.LeftItem, leftRight.RigthtItem, result);
      }
    }

    private class NodePathComparer : IEqualityComparer<NodeViewModel> {
      public static readonly NodePathComparer Instance = new NodePathComparer();

      public bool Equals(NodeViewModel x, NodeViewModel y) {
        return StringComparer.Ordinal.Equals(x.Path, y.Path);
      }

      public int GetHashCode(NodeViewModel obj) {
        return StringComparer.Ordinal.GetHashCode(obj.Path);
      }
    }

    private void BuildPathToNodeMap(NodeViewModel node) {
      if (!string.IsNullOrEmpty(node.Path)) {
        _pathToNode.Add(node.Path, node);
      }

      foreach (var child in node.Children) {
        BuildPathToNodeMap(child);
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

    private void AddRootEntry(FileSystemEntry root) {
      AddNodeForEntry(root, null);
    }

    private void AddNodeForEntry(FileSystemEntry entry, NodeViewModel parent) {
      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel()
        : (NodeViewModel)new FileNodeViewModel();

      var path = (parent == null ? entry.Name : PathHelpers.CombinePaths(parent.Path, entry.Name));

      if (_pathToNode.ContainsKey(path)) {
        var oldNode = _pathToNode[path];
        node.ItemId = oldNode.ItemId;
        node.IsExpanded = oldNode.IsExpanded;
      } else {
        node.ItemId = _newNodeNextItemId;
        _newNodeNextItemId++;
        node.IsExpanded = (parent == null);
      }

      node.Caption = entry.Name;
      node.Name = entry.Name;
      node.Path = path;
      node.ExpandByDefault = (parent == null);
      if (directoryEntry != null) {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphClosedFolder,
          StandardGlyphItem.GlyphItemPublic);
        node.OpenFolderImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphOpenFolder,
          StandardGlyphItem.GlyphItemPublic);

        foreach (var child in directoryEntry.Entries) {
          AddNodeForEntry(child, node);
        }
      } else {
        node.ImageIndex = _vsGlyphService.GetImageIndex(
          StandardGlyphGroup.GlyphCSharpFile,
          StandardGlyphItem.GlyphItemPublic);
      }

      _newNodes.AddNode(node, parent ?? _newNodes.RootNode);
    }
  }
}