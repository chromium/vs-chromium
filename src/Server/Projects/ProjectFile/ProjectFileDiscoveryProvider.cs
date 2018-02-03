// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Server.Projects.ProjectFile {
  /// <summary>
  /// Implementation of <see cref="IProjectDiscoveryProvider"/> looking for a
  /// project file "vs-chromium-project.txt" (or the obsoleted
  /// "project.vs-chromium-project") in the file system.
  /// </summary>
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ProjectFileDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly IFileSystem _fileSystem;
    private readonly FullPathDictionary<Project> _knownProjectRootDirectories = new FullPathDictionary<Project>();
    private readonly FullPathDictionary<object> _knownNonProjectPaths = new FullPathDictionary<object>();
    private readonly object _lock = new object();

    public int Priority { get { return 100; } }

    [ImportingConstructor]
    public ProjectFileDiscoveryProvider(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public IProject GetProjectFromAnyPath(FullPath path) {
      lock (_lock) {
        // Cache hit?
        var root = _knownProjectRootDirectories
          .Where(x => x.Key.ContainsPath(path))
          .OrderByDescending(x => x.Key)
          .FirstOrDefault();
        if (root.Key != default(FullPath)) {
          return root.Value;
        }

        // Negative cache hit?
        if (_knownNonProjectPaths.Contains(path)) {
          return null;
        }

        // Nope: compute all the way...
        return GetProjectWorker(path);
      }
    }

    public IProject GetProjectFromRootPath(FullPath projectRootPath) {
      var name = projectRootPath;
      lock (_lock) {
        return _knownProjectRootDirectories.Get(name);
      }
    }

    public void ValidateCache() {
      lock (_lock) {
        _knownProjectRootDirectories.Clear();
        _knownNonProjectPaths.Clear();
      }
    }

    private IProject GetProjectWorker(FullPath path) {
      var project = path
        .EnumeratePaths()
        .Select(CreateProject)
        .FirstOrDefault(x => x != null);
      if (project != null) {
        _knownProjectRootDirectories.Add(project.RootPath, project);
        return project;
      }

      // No one in the parent chain is a Chromium directory.
      path.EnumeratePaths().ForAll(x => _knownNonProjectPaths.Add(x, null));
      return null;
    }

    /// <summary>
    /// Create a project instance corresponding to the vschromium project file
    /// on disk at <paramref name="rootPath"/>.
    /// Return <code>null</code> if there is no project file.
    /// </summary>
    private Project CreateProject(FullPath rootPath) {
      var projectFilePath = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileName));
      var sectionName = ConfigurationSectionNames.SourceExplorerIgnore;
      if (!_fileSystem.FileExists(projectFilePath)) {
        projectFilePath = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileNameObsolete));
        sectionName = ConfigurationSectionNames.SourceExplorerIgnoreObsolete;
        if (!_fileSystem.FileExists(projectFilePath)) {
          return null;
        }
      }

      var fileWithSections = new FileWithSections(_fileSystem, projectFilePath);
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      var s1 = ConfigurationSectionContents.Create(configurationProvider, sectionName);
      var s2 = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesIgnore);
      var s3 = ConfigurationSectionContents.Create(configurationProvider, ConfigurationSectionNames.SearchableFilesInclude);
      var fileFilter = new FileFilter(s1);
      var directoryFilter = new DirectoryFilter(s1);
      var searchableFilesFilter = new SearchableFilesFilter(s2, s3);
      return new Project(rootPath, fileFilter, directoryFilter, searchableFilesFilter, fileWithSections.Hash);
    }
  }
}
