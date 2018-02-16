// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio;
using System;
using System.Collections.Generic;
using VsChromium.Core.Logging;

namespace VsChromium.Features.SourceExplorerHierarchy {
  /// <summary>
  /// The set of <see cref="NodeViewModel"/> nodes displayed in a single instance
  /// of <see cref="VsHierarchy"/>. The nodes have unique IDs requires for interacting
  /// with Visual Studio hierarchy API.
  /// 
  /// Intances of VsHierarchyNodes are not multi-thread safe, but thread ownerhip
  /// can change between initial construction and lazy exansion of nodes.
  /// 
  /// <para>Nodes are never removed, but can be added over time as the set of visible
  /// nodes increases (i.e. as <see cref="DirectoryNodeViewModel"/> are expanded
  /// </para>
  /// </summary>
  public class VsHierarchyNodes {
    public const uint RootNodeItemId = unchecked((uint)-2);

    private readonly Dictionary<uint, NodeViewModel> _itemIdMap = new Dictionary<uint, NodeViewModel>();
    private readonly NodeViewModel _rootNode;
    private uint _maxItemId = 5; // Arbitrary number not too close to 0.

    public VsHierarchyNodes() {
      _rootNode = new RootNodeViewModel();
      _rootNode.ItemId = RootNodeItemId;
      AddNode(_rootNode);
    }

    public NodeViewModel RootNode => _rootNode;
    public bool IsEmpty => _rootNode.GetChildrenCount() == 0;
    public uint MaxItemId => _maxItemId;
    public int Count => _itemIdMap.Count;


    public void AddNode(NodeViewModel node) {
      Invariants.Assert(node.ItemId != VSConstants.VSITEMID_NIL);
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
      node = null;
      if (itemid == 0 || itemid == VSConstants.VSITEMID_NIL || itemid == VSConstants.VSITEMID_SELECTION) {
        node = null;
        return false;
      }

      if (itemid == RootNodeItemId) {
        node = _rootNode;
        return true;
      }

      return _itemIdMap.TryGetValue(itemid, out node);
    }
  }
}
