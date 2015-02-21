// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalHierarchyBuilder {
    private readonly INodeTemplateFactory _templateFactory;
    private readonly VsHierarchyNodes _oldNodes;
    private readonly FileSystemTree _fileSystemTree;
    private readonly VsHierarchyNodes _newNodes = new VsHierarchyNodes();
    private readonly VsHierarchyChanges _changes = new VsHierarchyChanges();
    private readonly Dictionary<string, NodeViewModelTemplate> _fileTemplatesToInitialize =
      new Dictionary<string, NodeViewModelTemplate>(SystemPathComparer.Instance.StringComparer); 
    private uint _newNodeNextItemId;

    public IncrementalHierarchyBuilder(
      INodeTemplateFactory nodeTemplateFactory,
      VsHierarchyNodes oldNodes,
      FileSystemTree fileSystemTree) {
      _templateFactory = nodeTemplateFactory;
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
          FileTemplatesToInitialize = _fileTemplatesToInitialize
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
        foreach (var childEntry in directoryEntry.Entries.ToEnumerator()) {
          var child = CreateNodeViewModel(childEntry, newParent);
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

      foreach (var item in diffs.LeftOnlyItems.ToEnumerator()) {
        _changes.DeletedItems.Add(item.ItemId);
      }

      foreach(var newChild in diffs.RightOnlyItems.ToEnumerator()) {
        newChild.ItemId = _newNodeNextItemId;
        _newNodeNextItemId++;
        newChild.IsExpanded = newParent.IsRoot;
        _newNodes.AddNode(newChild);

        if (oldParent != null) {
          _changes.AddedItems.Add(newChild.ItemId);
        }
      }

      foreach (var pair in diffs.CommonItems.ToEnumerator()) {
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

      foreach (var pair in diffs.CommonItems.ToEnumerator()) {
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
      if (parent.IsRoot) {
        node.Template = _templateFactory.ProjectTemplate;
      } else if (directoryEntry != null) {
        node.Template = _templateFactory.DirectoryTemplate;
      } else {
        var extension = Path.GetExtension(entry.Name);
        Debug.Assert(extension != null);
        node.Template = _templateFactory.GetFileTemplate(extension);
        if (node.Template.Icon == null) {
          if (!_fileTemplatesToInitialize.ContainsKey(extension)) {
            _fileTemplatesToInitialize.Add(extension, node.Template);
          }
        }
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
      root.Template = _templateFactory.RootNodeTemplate;
    }
  }
}