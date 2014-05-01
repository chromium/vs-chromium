// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Features.ToolWindows.SourceExplorer {
  public class SourceExplorerItemViewModelBase : TreeViewItemViewModel {
    private readonly ISourceExplorerItemViewModelHost _host;

    public SourceExplorerItemViewModelBase(
        ISourceExplorerItemViewModelHost host,
        TreeViewItemViewModel parent,
        bool lazyLoadChildren)
      : base(host.StandarImageSourceFactory, parent, lazyLoadChildren) {
      _host = host;
    }

    public ISourceExplorerItemViewModelHost Host { get { return _host; } }
  }
}
