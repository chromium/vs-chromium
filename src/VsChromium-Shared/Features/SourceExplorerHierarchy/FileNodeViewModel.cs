// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using VsChromium.Core.Collections;
using VsChromium.Core.Logging;

namespace VsChromium.Features.SourceExplorerHierarchy {
  public class FileNodeViewModel : NodeViewModel {
    public FileNodeViewModel(NodeViewModel parent) : base(parent) {
      Invariants.CheckArgumentNotNull(parent, nameof(parent));
    }

    protected override IList<NodeViewModel> ChildrenImpl => ArrayUtilities.EmptyList<NodeViewModel>.Instance;
    protected override bool IsExpandable => false;

    protected override void AddChildImpl(NodeViewModel node) {
      Invariants.CheckOperation(false, "Cannot add child node to a file node");
    }
  }
}