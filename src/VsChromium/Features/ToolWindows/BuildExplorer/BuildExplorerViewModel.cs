// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.ComponentModelHost;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VsChromium.Core.Chromium;
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  class BuildExplorerViewModel {
    private TreeViewRootNodes<ITreeViewItem> _rootNodes = new TreeViewRootNodes<ITreeViewItem>();
    private ITreeViewItem _installedBuildsNode = null;
    private ITreeViewItem _developerBuildsNode = null;

    public TreeViewRootNodes<ITreeViewItem> RootNodes { get { return _rootNodes; } }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool AutoAttach { get; set; }

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      _installedBuildsNode = new SimpleTreeViewItem("Installed Builds", null);
      _developerBuildsNode = new SimpleTreeViewItem("Developer Builds", null);

      _rootNodes.Add(_installedBuildsNode);
      _rootNodes.Add(_developerBuildsNode);

      InstallationEnumerator enumerator = new InstallationEnumerator();
      foreach (InstallationData data in enumerator)
        _installedBuildsNode.Children.Add(new InstallationTreeViewItem(data));
    }
  }
}
