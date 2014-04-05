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
  public class BuildExplorerViewModel {
    private List<InstalledBuildViewModel> _installedBuilds = new List<InstalledBuildViewModel>();
    private List<DeveloperBuildViewModel> _developerBuilds = new List<DeveloperBuildViewModel>();

    public IList<InstalledBuildViewModel> InstalledBuilds { get { return _installedBuilds; } }
    public IList<DeveloperBuildViewModel> DeveloperBuilds { get { return _developerBuilds; } }

    /// <summary>
    /// Databound!
    /// </summary>
    public bool AutoAttach { get; set; }

    public void OnToolWindowCreated(IServiceProvider serviceProvider) {
      InstallationEnumerator enumerator = new InstallationEnumerator();
      foreach (InstallationData data in enumerator) {
        InstalledBuildViewModel build = new InstalledBuildViewModel(this, data);
        _installedBuilds.Add(build);
      }

      ReloadProcesses();
    }

    private void ReloadProcesses() {
      foreach (InstalledBuildViewModel installedBuild in _installedBuilds)
        installedBuild.LoadProcesses();
    }
  }
}
