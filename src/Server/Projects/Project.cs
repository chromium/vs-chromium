// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Configuration;
using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public class Project : IProject {
    private readonly FullPath _rootPath;
    private readonly IConfigurationSectionContents _pathsIgnoreConfiguration;
    private readonly IConfigurationSectionContents _searchableFilesIgnoreConfiguration;
    private readonly IConfigurationSectionContents _searchableFilesIncludeConfiguration;
    private readonly IDirectoryFilter _directoryFilter;
    private readonly IFileFilter _fileFilter;
    private readonly ISearchableFilesFilter _searchableFilesFilter;
    private readonly string _hash;

    public Project(
      FullPath rootPath,
      IConfigurationSectionContents pathsIgnoreConfiguration,
      IConfigurationSectionContents searchableFilesIgnoreConfiguration,
      IConfigurationSectionContents searchableFilesIncludeConfiguration,
      IFileFilter fileFilter,
      IDirectoryFilter directoryFilter,
      ISearchableFilesFilter searchableFilesFilter,
      string hash) {
      _rootPath = rootPath;
      _pathsIgnoreConfiguration = pathsIgnoreConfiguration;
      _searchableFilesIncludeConfiguration = searchableFilesIncludeConfiguration;
      _searchableFilesIgnoreConfiguration = searchableFilesIgnoreConfiguration;
      _directoryFilter = directoryFilter;
      _fileFilter = fileFilter;
      _searchableFilesFilter = searchableFilesFilter;
      _hash = hash;
    }

    public FullPath RootPath => _rootPath;
    public IFileFilter FileFilter => _fileFilter;
    public IDirectoryFilter DirectoryFilter => _directoryFilter;
    public ISearchableFilesFilter SearchableFilesFilter => _searchableFilesFilter;
    public IConfigurationSectionContents IgnorePathsConfiguration => _pathsIgnoreConfiguration;
    public IConfigurationSectionContents IgnoreSearchableFilesConfiguration => _searchableFilesIgnoreConfiguration;
    public IConfigurationSectionContents IncludeSearchableFilesConfiguration => _searchableFilesIncludeConfiguration;
    public string VersionHash => _hash;
  }
}