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
    private readonly IChromiumDiscoveryWithCache<Project> _chromiumDiscovery;

    [ImportingConstructor]
    public ChromiumProjectDiscoveryProvider(IConfigurationFileProvider configurationFileProvider, IFileSystem fileSystem) {
      _configurationSectionProvider = new ConfigurationFileSectionProvider(configurationFileProvider);
      _chromiumDiscovery = new ChromiumDiscoveryWithCache<Project>(_configurationSectionProvider, fileSystem);
    }

    public IProject GetProjectFromRootPath(FullPath projectRootPath) {
      return _chromiumDiscovery.GetEnlistmentRootFromRootpath(projectRootPath, CreateProject);
    }

    public int Priority { get { return -100; } }

    public IProject GetProject(FullPath filename) {
      return _chromiumDiscovery.GetEnlistmentRootFromFilename(filename, CreateProject);
    }

    public void ValidateCache() {
      _chromiumDiscovery.ValidateCache();
    }

    private Project CreateProject(FullPath rootPath) {
      return new Project(_configurationSectionProvider, rootPath);
    }
  }
}
