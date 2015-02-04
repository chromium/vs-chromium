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
  /// Implementation of <see cref="IProjectDiscoveryProvider"/> looking for
  /// project file in the file system.
  /// </summary>
  [Export(typeof(IProjectDiscoveryProvider))]
  public class ProjectFileDiscoveryProvider : IProjectDiscoveryProvider {
    private readonly IFileSystem _fileSystem;
    private readonly FullPathDictionary<Project> _knownProjectRootDirectories = new FullPathDictionary<Project>();
    private readonly FullPathDictionary<object> _knownNonProjectDirectories = new FullPathDictionary<object>();
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
          .Where(x => PathHelpers.IsPrefix(path.Value, x.Key.Value))
          .OrderByDescending(x => x.Key.Value.Length)
          .FirstOrDefault();
        if (root.Key != default(FullPath)) {
          return root.Value;
        }

        // Negative cache hit?
        if (_knownNonProjectDirectories.Contains(path.Parent)) {
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
        _knownNonProjectDirectories.Clear();

      }
    }

    private IProject GetProjectWorker(FullPath filepath) {
      var directory = filepath.Parent;
      if (_fileSystem.DirectoryExists(directory)) {
        var project = filepath.EnumerateParents().Select(CreateProject).FirstOrDefault();
        if (project != null) {
          _knownProjectRootDirectories.Add(project.RootPath, project);
          return project;
        }
      }

      // No one in the parent chain is a Chromium directory.
      filepath.EnumerateParents().ForAll(x => _knownNonProjectDirectories.Add(x, null));
      return null;
    }

    /// <summary>
    /// Create a project instance corresponding to the vschromium project file
    /// on disk at <paramref name="rootPath"/>.
    /// </summary>
    private Project CreateProject(FullPath rootPath) {
      var path = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileName));
      if (!_fileSystem.FileExists(path)) {
        path = rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileNameObsolete));
        if (!_fileSystem.FileExists(path)) {
          return null;
        }
      }

      var fileWithSections = new FileWithSections(_fileSystem, path);
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      return new Project(configurationProvider, rootPath);
    }
  }
}
