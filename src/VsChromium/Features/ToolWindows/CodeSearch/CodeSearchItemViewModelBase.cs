// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.ToolWindows.CodeSearch {
  public class CodeSearchItemViewModelBase : TreeViewItemViewModel {
    private readonly ICodeSearchController _controller;

    public CodeSearchItemViewModelBase(
        ICodeSearchController controller,
        TreeViewItemViewModel parent,
        bool lazyLoadChildren)
      : base(controller.StandarImageSourceFactory, parent, lazyLoadChildren) {
      _controller = controller;
      var con = controller as CodeSearchController;
      if (con != null && con.ViewModel.ExpandAll)
        LazySelect = (TreeViewItemViewModel x) => { ExpandAll(x); };
    }

    public ICodeSearchController Controller { get { return _controller; } }
  }
}
