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
using VsChromium.Views;

namespace VsChromium.Features.ToolWindows.BuildExplorer {
  class BuildExplorerViewModel : ChromiumExplorerViewModelBase {
    private List<TreeViewItemViewModel> _categoryRootNodes = new List<TreeViewItemViewModel>();
    private InstalledBuildsCategoryItemViewModel _installedBuildsNode = null;
    private DeveloperBuildsCategoryItemViewModel _developerBuildsNode = null;

    /// <summary>
    /// Databound!
    /// </summary>
    public bool AutoAttach { get; set; }

    public override void OnToolWindowCreated(IServiceProvider serviceProvider) {
      base.OnToolWindowCreated(serviceProvider);

      _installedBuildsNode = new InstalledBuildsCategoryItemViewModel(ImageSourceFactory);
      _developerBuildsNode = new DeveloperBuildsCategoryItemViewModel(ImageSourceFactory);

      _categoryRootNodes.Add(_installedBuildsNode);
      _categoryRootNodes.Add(_developerBuildsNode);

      SetRootNodes(_categoryRootNodes);
    }
  }
}
