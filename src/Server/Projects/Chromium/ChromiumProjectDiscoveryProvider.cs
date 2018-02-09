// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

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
      return _chromiumDiscovery.GetEnlistmentRootFromAnyPath(path, CreateProject);
    }

    public void ValidateCache() {
      _chromiumDiscovery.ValidateCache();
    }

    private Project CreateProject(FullPath rootPath) {
      var configurationProvider = _configurationSectionProvider;
      var ignorePathsSection = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SourceExplorerIgnoreObsolete);
      var ignoreSearchableFilesSection = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesIgnore);
      var includeSearchableFilesSection = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesInclude);
      var fileFilter = new FileFilter(ignorePathsSection);
      var directoryFilter = new DirectoryFilter(ignorePathsSection);
      var searchableFilesFilter = new SearchableFilesFilter(ignoreSearchableFilesSection, includeSearchableFilesSection);
      var hash = MD5Hash.CreateHash(ignorePathsSection.Contents.Concat(ignoreSearchableFilesSection.Contents).Concat(includeSearchableFilesSection.Contents));
      return new Project(rootPath, ignorePathsSection, ignoreSearchableFilesSection, includeSearchableFilesSection, fileFilter, directoryFilter, searchableFilesFilter, hash);
    }
  }
}
