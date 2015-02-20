// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.VisualStudio;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class VsHierarchyNodes {
    public const uint RootNodeItemId = unchecked((uint)-2);
    private readonly Dictionary<uint, NodeViewModel> _itemIdMap = new Dictionary<uint, NodeViewModel>();
    private readonly NodeViewModel _rootNode = new NodeViewModel();
    private uint _maxItemId = 5; // Arbitrary number not too close to 0.

    public VsHierarchyNodes() {
      _rootNode.ItemId = RootNodeItemId;
      AddNode(_rootNode);
    }

    public NodeViewModel RootNode { get { return _rootNode; } }

    public uint MaxItemId { get { return _maxItemId; } }
    public int Count { get { return _itemIdMap.Count; } }

    public void AddNode(NodeViewModel node) {
      Debug.Assert(node.ItemId != VSConstants.VSITEMID_NIL);
      _itemIdMap.Add(node.ItemId, node);
      if (node.ItemId != RootNodeItemId) {
        _maxItemId = Math.Max(_maxItemId, node.ItemId);
      }
    }

    public NodeViewModel GetNode(uint itemid) {
      NodeViewModel result;
      if (!FindNode(itemid, out result))
        return null;
      return result;
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
