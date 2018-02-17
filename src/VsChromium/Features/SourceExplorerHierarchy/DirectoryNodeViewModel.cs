// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class DirectoryNodeViewModel : NodeViewModel {
    private readonly List<NodeViewModel> _childrenList = new List<NodeViewModel>();

    public DirectoryNodeViewModel(NodeViewModel parent) : base(parent) {
    }

    protected override IList<NodeViewModel> ChildrenImpl => _childrenList;

    protected override bool IsExpandable => _childrenList.Count > 0 || !ChildrenLoaded;

    protected override void AddChildImpl(NodeViewModel node) {
      ChildrenLoaded = true;
      node.ChildIndex = _childrenList.Count;
      _childrenList.Add(node);
    }

    public List<NodeViewModel> CopyChildren() {
      lock (_childrenList) {
        return new List<NodeViewModel>(_childrenList);
      }
    }

    public void SetChildren(List<NodeViewModel> children) {
      ChildrenLoaded = true;
      lock (_childrenList) {
        foreach (var child in children) {
          AddChildImpl(child);
        }
      }
    }
  }
}