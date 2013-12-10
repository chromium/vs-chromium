// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromiumCore.Configuration;
using VsChromiumCore.FileNames;

namespace VsChromiumServer.Projects.ProjectFile {
  class ProjectFileProject : IProject {
    private readonly FullPathName _rootPath;
    private readonly IDirectoryFilter _directoryFilter;
    private readonly IFileFilter _fileFilter;
    private readonly ISearchableFilesFilter _searchableFilesFilter;

    public ProjectFileProject(IConfigurationSectionProvider configurationSectionProvider, FullPathName rootPath) {
      _rootPath = rootPath;
      _directoryFilter = new DirectoryFilter(configurationSectionProvider);
      _fileFilter = new FileFilter(configurationSectionProvider);
      _searchableFilesFilter = new SearchableFilesFilter(configurationSectionProvider);
    }

    public string RootPath { get { return _rootPath.FullName; } }

    public IDirectoryFilter DirectoryFilter { get { return _directoryFilter; } }

    public IFileFilter FileFilter { get { return _fileFilter; } }

    public ISearchableFilesFilter SearchableFilesFilter { get { return _searchableFilesFilter; } }
  }
}
