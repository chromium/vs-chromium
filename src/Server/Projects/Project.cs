// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;

namespace VsChromium.Server.Projects {
  public class Project : IProject {
    private readonly FullPath _rootPath;
    private readonly IDirectoryFilter _directoryFilter;
    private readonly IFileFilter _fileFilter;
    private readonly ISearchableFilesFilter _searchableFilesFilter;
    private readonly string _hash;

    public Project(
      FullPath rootPath,
      IFileFilter fileFilter,
      IDirectoryFilter directoryFilter,
      ISearchableFilesFilter searchableFilesFilter,
      string hash) {
      _rootPath = rootPath;
      _directoryFilter = directoryFilter;
      _fileFilter = fileFilter;
      _searchableFilesFilter = searchableFilesFilter;
      _hash = hash;
    }

    public FullPath RootPath { get { return _rootPath; } }
    public IFileFilter FileFilter { get { return _fileFilter; } }
    public IDirectoryFilter DirectoryFilter { get { return _directoryFilter; } }
    public ISearchableFilesFilter SearchableFilesFilter { get { return _searchableFilesFilter; } }
    public string VersionHash { get { return _hash; } }
  }
}