// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyNodes {
    public const uint RootNodeItemId = unchecked((uint)-2);
    private readonly VsHierarchy _hierarchy;
    private readonly Dictionary<uint, NodeViewModel> _itemIdMap = new Dictionary<uint, NodeViewModel>();
    private readonly NodeViewModel _rootNode;

    // TODO: Remove "hierarchy" param.
    public VsHierarchyNodes(VsHierarchy hierarchy) {
      _hierarchy = hierarchy;
      _rootNode = new NodeViewModel(hierarchy);
      AddNode(_rootNode, null);
    }

    public NodeViewModel RootNode { get { return _rootNode; } }

    public void Clear() {
      _itemIdMap.Clear();
      _rootNode.RemoveChildren();
    }

    public void AddNode(NodeViewModel node, NodeViewModel parent) {
      var itemId = (_itemIdMap.Count == 0)
        ? RootNodeItemId 
        : (uint)(_itemIdMap.Count + 1);

      // Set ItemId and add to map
      node.ItemId = itemId;
      _itemIdMap.Add(itemId, node);


      // Add to children
      if (RootNodeItemId != itemId) {
        if (parent == null)
          parent = _rootNode;
        parent.AddChild(node);
      }
    }

    public bool FindNode(uint itemid, out NodeViewModel node) {
      node = (NodeViewModel)null;
      if ((int)itemid == 0 || (int)itemid == -1 || (int)itemid == -3)
        return false;
      if (itemid != RootNodeItemId)
        return _itemIdMap.TryGetValue(itemid, out node);
      node = _rootNode;
      return node != null;
    }
  }
}
