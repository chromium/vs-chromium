// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class RootNodeViewModel : NodeViewModel {
    private readonly List<NodeViewModel> _childrenList = new List<NodeViewModel>();

    protected override IList<NodeViewModel> ChildrenImpl {
      get { return _childrenList; }
    }
  }
}