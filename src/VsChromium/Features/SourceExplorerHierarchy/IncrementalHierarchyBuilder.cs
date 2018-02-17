// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Views;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class IncrementalHierarchyBuilder : IIncrementalHierarchyBuilder {
    private readonly INodeTemplateFactory _templateFactory;
    private readonly VsHierarchy _hierarchy;
    private readonly VsHierarchyNodes _oldNodes;
    private readonly FullPath _projectPath;
    private readonly int _treeVersion;
    private readonly INodeViewModelLoader _nodeViewModelLoader;
    private readonly IImageSourceFactory _imageSourceFactory;
    private readonly VsHierarchyNodes _newNodes = new VsHierarchyNodes();
    private readonly VsHierarchyChanges _changes = new VsHierarchyChanges();

    private readonly Dictionary<string, NodeViewModelTemplate> _fileTemplatesToInitialize =
      new Dictionary<string, NodeViewModelTemplate>(SystemPathComparer.Instance.StringComparer);

    private uint _newNodeNextItemId;

    public IncrementalHierarchyBuilder(
      INodeTemplateFactory nodeTemplateFactory,
      VsHierarchy hierarchy,
      FullPath projectPath,
      int treeVersion,
      INodeViewModelLoader nodeViewModelLoader,
      IImageSourceFactory imageSourceFactory) {
      _templateFactory = nodeTemplateFactory;
      _hierarchy = hierarchy;
      _oldNodes = hierarchy.Nodes;
      _projectPath = projectPath;
      _treeVersion = treeVersion;
      _nodeViewModelLoader = nodeViewModelLoader;
      _imageSourceFactory = imageSourceFactory;
    }

    public Func<int, ApplyChangesResult> ComputeChangeApplier() {
      // Capture hierarchy version # for checking later that another
      // thread did not beat us.
      int hierarchyVersion = _hierarchy.Version;

      // Build the new nodes
      var buildResult = RunImpl();

      // Return the predicate to run on the main thread.
      return latestFileSystemTreeVersion =>
        ApplyChanges(buildResult, hierarchyVersion, latestFileSystemTreeVersion);
    }

    private ApplyChangesResult ApplyChanges(IncrementalBuildResult buildResult, int hierarchyVersion,
      int latestFileSystemTreeVersion) {
      // We need to load these images on the main UI thread
      buildResult.FileTemplatesToInitialize.ForAll(item => {
        item.Value.Icon = _imageSourceFactory.GetFileExtensionIcon(item.Key);
      });

      // Apply if nobody beat us to is.
      if (_hierarchy.Version == hierarchyVersion) {
        Logger.LogInfo(
          "Updating VsHierarchy nodes for version {0} and file system tree version {1}",
          hierarchyVersion,
          _treeVersion);
        _hierarchy.SetNodes(buildResult.NewNodes, buildResult.Changes);
        return ApplyChangesResult.Done;
      }

      Logger.LogInfo(
        "VsHierarchy nodes have been updated concurrently, re-run or skip operation." +
        " Node verions={0}-{1}, Tree versions:{2}-{3}.",
        hierarchyVersion, _hierarchy.Version,
        _treeVersion, latestFileSystemTreeVersion);

      // If the version of the hieararchy has changed since when we started,
      // another thread has passed us.  This means the decisions we made
      // about the changes to apply are incorrect at this point. So, we run
      // again if we are processing the latest known version of the file
      // system tree, as we should be the winner (eventually)
      if (_treeVersion == latestFileSystemTreeVersion) {
        // Termination notes: We make this call only when the VsHierarchy
        // version changes between the time we capture it and this point.
        return ApplyChangesResult.Retry;
      }

      return ApplyChangesResult.Bail;
    }

    private IncrementalBuildResult RunImpl() {
      using (new TimeElapsedLogger("Computing NodesViewModel with diffs", InfoLogger.Instance)) {
        _newNodeNextItemId = _oldNodes.MaxItemId + 1;

        SetupRootNode(_newNodes.RootNode);

        // Update root node
        var rootNode = _newNodes.RootNode;
        rootNode.Name = _projectPath.Value;
        rootNode.Caption = string.Format("Source Explorer - {0}", _projectPath.Value);
        rootNode.Template = _templateFactory.ProjectTemplate;

        // Add children nodes
        AddNodeForChildren(_oldNodes.RootNode, _newNodes.RootNode);

        return new IncrementalBuildResult {
          OldNodes = _oldNodes,
          NewNodes = _newNodes,
          Changes = _changes,
          FileTemplatesToInitialize = _fileTemplatesToInitialize
        };
      }
    }

    private void AddNodeForChildren(NodeViewModel oldParent, NodeViewModel newParent) {
      Invariants.Assert(oldParent != null);
      Invariants.Assert(newParent != null);
      Invariants.Assert(newParent.Children.Count == 0);

      var oldParentDirectory = oldParent as DirectoryNodeViewModel;
      if (oldParentDirectory == null) {
        return;
      }

      var newParentDirectory = newParent as DirectoryNodeViewModel;
      if (newParentDirectory == null) {
        return;
      }

      var oldParentChildrenList = oldParentDirectory.CopyChildren();
      if (oldParentChildrenList.Count == 0 && !oldParent.IsRoot) {
        return;
      }

      // Create children nodes
      var directoryEntry = _nodeViewModelLoader.LoadChildrenAsync(newParentDirectory).Result;
      if (directoryEntry != null) {
        foreach (var childEntry in directoryEntry.Entries.ToForeachEnum()) {
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
      var diffs = ArrayUtilities.BuildArrayDiffs(oldParentChildrenList, newParent.Children,
        NodeTypeAndNameComparer.Instance);

      // Mark deleted items
      foreach (var item in diffs.LeftOnlyItems.ToForeachEnum()) {
        _changes.DeletedItems.Add(item.ItemId);
      }

      // Mark added items
      foreach (var newChild in diffs.RightOnlyItems.ToForeachEnum()) {
        newChild.ItemId = _newNodeNextItemId;
        _newNodeNextItemId++;
        newChild.IsExpanded = newParent.IsRoot;
        _newNodes.AddNode(newChild);

        _changes.AddedItems.Add(newChild.ItemId);
      }

      // Commons items don't need updating, just adding to the new parent
      foreach (var pair in diffs.CommonItems.ToForeachEnum()) {
        pair.RigthtItem.ItemId = pair.LeftItem.ItemId;
        pair.RigthtItem.IsExpanded = pair.LeftItem.IsExpanded;
        _newNodes.AddNode(pair.RigthtItem);
      }

      // Call recursively on all children
      if (directoryEntry != null) {
        Invariants.Assert(directoryEntry.Entries.Count == newParent.Children.Count);
        for (var i = 0; i < newParent.Children.Count; i++) {
          var newChildNode = newParent.Children[i];
          var oldChildNode = GetCommonOldNode(newParent, i, diffs, newChildNode);

          if (oldChildNode != null) {
            AddNodeForChildren(oldChildNode, newChildNode);
          }
        }
      }
    }

    private static NodeViewModel GetCommonOldNode(NodeViewModel newParent, int index,
      ArrayDiffsResult<NodeViewModel> diffs, NodeViewModel newChildNode) {
      if (diffs.CommonItems.Count == newParent.Children.Count) {
        return diffs.CommonItems[index].LeftItem;
      }

      foreach (var pair in diffs.CommonItems.ToForeachEnum()) {
        if (pair.RigthtItem == newChildNode)
          return pair.LeftItem;
      }

      return null;
    }

    private NodeViewModel CreateNodeViewModel(FileSystemEntry entry, NodeViewModel parent) {
      Invariants.Assert(entry != null);
      Invariants.Assert(parent != null);

      var node = CreateNodeViewModel(_templateFactory, entry, parent);
      if (node is FileNodeViewModel) {
        if (node.Template.Icon == null) {
          var extension = Path.GetExtension(entry.Name);
          Invariants.Assert(extension != null);
          if (!_fileTemplatesToInitialize.ContainsKey(extension)) {
            _fileTemplatesToInitialize.Add(extension, node.Template);
          }
        }
      }

      return node;
    }

    public static NodeViewModel CreateNodeViewModel(INodeTemplateFactory templateFactory, FileSystemEntry entry,
      NodeViewModel parent) {
      Invariants.Assert(entry != null);
      Invariants.Assert(parent != null);

      var directoryEntry = entry as DirectoryEntry;
      var node = directoryEntry != null
        ? (NodeViewModel)new DirectoryNodeViewModel(parent)
        : (NodeViewModel)new FileNodeViewModel(parent);

      node.Caption = entry.Name;
      node.Name = entry.Name;
      if (PathHelpers.IsAbsolutePath(node.Name)) {
        node.Template = templateFactory.ProjectTemplate;
      } else if (directoryEntry != null) {
        node.Template = templateFactory.DirectoryTemplate;
      } else {
        var extension = Path.GetExtension(entry.Name);
        Invariants.Assert(extension != null);
        node.Template = templateFactory.GetFileTemplate(extension);
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
      var name = "Source Explorer - VS Chromium Projects";
      root.Name = name;
      root.Caption = name;
      root.Template = _templateFactory.RootNodeTemplate;
    }
  }
}