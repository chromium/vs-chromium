// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Chromium;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Utility;

namespace VsChromium.Server.Projects.Chromium {
  /// <summary>
  /// Implementation of <see cref="IProjectDiscoveryProvider"/> looking for
  /// Chromium enlistments in the file system.
  /// </summary>
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ChromiumProjectDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly IConfigurationSectionProvider _configurationSectionProvider;
    private readonly IChromiumDiscoveryWithCache<Project> _chromiumDiscovery;

    [ImportingConstructor]
    public ChromiumProjectDiscoveryProvider(IConfigurationFileLocator configurationFileLocator, IFileSystem fileSystem) {
      _configurationSectionProvider = new ConfigurationFileConfigurationSectionProvider(configurationFileLocator);
      _chromiumDiscovery = new ChromiumDiscoveryWithCache<Project>(_configurationSectionProvider, fileSystem);
    }

    public IProject GetProjectFromRootPath(FullPath projectRootPath) {
      return _chromiumDiscovery.GetEnlistmentRootFromRootpath(projectRootPath, CreateProject);
    }

    public int Priority { get { return -100; } }

    public IProject GetProjectFromAnyPath(FullPath path) {
      return _chromiumDiscovery.GetEnlistmentRootFromFilename(path, CreateProject);
    }

    public void ValidateCache() {
      _chromiumDiscovery.ValidateCache();
    }

    private Project CreateProject(FullPath rootPath) {
      var configurationProvider = _configurationSectionProvider;
      var section1 = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SourceExplorerIgnoreObsolete);
      var section2 = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesIgnore);
      var section3 = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesInclude);
      var fileFilter = new FileFilter(section1);
      var directoryFilter = new DirectoryFilter(section1);
      var searchableFilesFilter = new SearchableFilesFilter(section2, section3);
      var hash = MD5Hash.CreateHash(section1.Contents.Concat(section2.Contents).Concat(section3.Contents));
      return new Project(rootPath, fileFilter, directoryFilter, searchableFilesFilter, hash);
    }
  }
}
