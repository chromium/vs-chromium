// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class SourceExplorerItemViewModelBase : TreeViewItemViewModel {
    private readonly ISourceExplorerController _controller;

    public SourceExplorerItemViewModelBase(
        ISourceExplorerController controller,
        TreeViewItemViewModel parent,
        bool lazyLoadChildren)
      : base(controller.StandarImageSourceFactory, parent, lazyLoadChildren) {
      _controller = controller;
    }

    public ISourceExplorerController Controller { get { return _controller; } }
  }
}
