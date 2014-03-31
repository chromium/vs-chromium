// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Chromium;
using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;

namespace VsChromium.Server.Projects.Chromium {
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ChromiumProjectDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly IConfigurationSectionProvider _configurationSectionProvider;
    private readonly IChromiumDiscoveryWithCache<ChromiumProject> _chromiumDiscovery;

    [ImportingConstructor]
    public ChromiumProjectDiscoveryProvider(IConfigurationFileProvider configurationFileProvider) {
      _configurationSectionProvider = new ConfigurationFileSectionProvider(configurationFileProvider);
      _chromiumDiscovery = new ChromiumDiscoveryWithCache<ChromiumProject>(_configurationSectionProvider);
    }

    public IProject GetProjectFromRootPath(FullPathName projectRootPath) {
      return _chromiumDiscovery.GetEnlistmentRootFromRootpath(projectRootPath, CreateProject);
    }

    public int Priority { get { return -100; } }

    public IProject GetProject(FullPathName filename) {
      return _chromiumDiscovery.GetEnlistmentRootFromFilename(filename, CreateProject);
    }

    public void ValidateCache() {
      _chromiumDiscovery.ValidateCache();
    }

    private ChromiumProject CreateProject(FullPathName rootPath) {
      return new ChromiumProject(_configurationSectionProvider, rootPath.FullName);
    }
  }
}
