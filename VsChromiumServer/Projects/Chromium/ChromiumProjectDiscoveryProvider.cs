// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromiumCore.Chromium;
using VsChromiumCore.Configuration;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.Projects.Chromium {
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ChromiumProjectDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly IConfigurationSectionProvider _configurationSectionProvider;
    private readonly IChromiumDiscoveryWithCache<ChromiumProject> _chromiumDiscovery;

    [ImportingConstructor]
    public ChromiumProjectDiscoveryProvider(IConfigurationFileProvider configurationFileProvider) {
      _configurationSectionProvider = new ConfigurationFileSectionProvider(configurationFileProvider);
      _chromiumDiscovery = new ChromiumDiscoveryWithCache<ChromiumProject>(_configurationSectionProvider);
    }

    public IProject GetProjectFromRootPath(string projectRootPath) {
      return _chromiumDiscovery.GetEnlistmentRootFromRootpath(new FullPathName(projectRootPath), CreateProject);
    }

    public IProject GetProject(string filename) {
      return _chromiumDiscovery.GetEnlistmentRootFromFilename(new FullPathName(filename), CreateProject);
    }

    public void ValidateCache() {
      _chromiumDiscovery.ValidateCache();
    }

    private ChromiumProject CreateProject(FullPathName rootPath) {
      return new ChromiumProject(_configurationSectionProvider, rootPath.FullName);
    }
  }
}
