// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows {
  public class RootTreeViewItemViewModel : TreeViewItemViewModel {
    private readonly List<TreeViewItemViewModel> _rootNodes = new List<TreeViewItemViewModel>();

    public RootTreeViewItemViewModel(IStandarImageSourceFactory imageSourceFactory)
      : base(imageSourceFactory, null, true) {
    }

    public void AddChild(TreeViewItemViewModel node) {
      _rootNodes.Add(node);
    }

    protected override IEnumerable<TreeViewItemViewModel> GetChildren() {
      return _rootNodes;
    }

    protected override bool IsVisual {
      get { return false; }
    }
  }
}