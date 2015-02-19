// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyNodes {
    public const uint RootNodeItemId = unchecked((uint)-2);
    private readonly Dictionary<uint, NodeViewModel> _itemIdMap = new Dictionary<uint, NodeViewModel>();
    private readonly NodeViewModel _rootNode = new NodeViewModel();
    private uint _nextItemId = 5; // Arbitrary number not too close to 0.

    public VsHierarchyNodes() {
      AddNodeImpl(_rootNode, null);
    }

    public NodeViewModel RootNode { get { return _rootNode; } }

    public uint MaxItemId { get { return _nextItemId; } }

    public void AddNode(NodeViewModel node, NodeViewModel parent) {
      if (node == null)
        throw new ArgumentNullException();
      if (parent == null)
        throw new ArgumentNullException();
      AddNodeImpl(node, parent);
    }

    private void AddNodeImpl(NodeViewModel node, NodeViewModel parent) {
      if (_itemIdMap.Count == 0) {
        node.ItemId = RootNodeItemId;
      } else if (node.ItemId == uint.MaxValue) {
        node.ItemId = _nextItemId;
        _nextItemId++;
      }

      _itemIdMap.Add(node.ItemId, node);

      // Add to children
      if (parent != null) {
        parent.AddChild(node);
      }
    }

    public bool FindNode(uint itemid, out NodeViewModel node) {
      node = (NodeViewModel)null;
      if (itemid == 0 || itemid == VSConstants.VSITEMID_NIL || itemid == VSConstants.VSITEMID_SELECTION)
        return false;
      if (itemid != RootNodeItemId)
        return _itemIdMap.TryGetValue(itemid, out node);
      node = _rootNode;
      return node != null;
    }
  }
}
