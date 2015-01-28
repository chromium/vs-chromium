// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;

namespace VsChromium.Server.Projects.ProjectFile {
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
        //_knownProjectRootDirectories.RemoveWhere(x => !_fileSystem.DirectoryExists(x.Key));
        //_knownProjectRootDirectories.RemoveWhere(x => x.Value.IsOutdated);
        //_knownNonProjectDirectories.RemoveWhere(x => !_fileSystem.DirectoryExists(x.Key));
      }
    }

    private IProject GetProjectWorker(FullPath filepath) {
      var directory = filepath.Parent;
      if (_fileSystem.DirectoryExists(directory)) {
        var projectPath = EnumerateParents(filepath).FirstOrDefault(x => ContainsProjectFile(x));
        if (projectPath != default(FullPath)) {
          var project = CreateProject(projectPath);
          _knownProjectRootDirectories.Add(projectPath, project);
          return project;
        }
      }

      // No one in the parent chain is a Chromium directory.
      EnumerateParents(filepath).ForAll(x => _knownNonProjectDirectories.Add(x, null));
      return null;
    }

    /// <summary>
    /// Create a project instance corresponding to the vschromium project file
    /// on disk at <paramref name="rootPath"/>.
    /// </summary>
    private Project CreateProject(FullPath rootPath) {
      var fileWithSections = new FileWithSections(
        _fileSystem,
        rootPath.Combine(new RelativePath(ConfigurationFileNames.ProjectFileNameDetection)));
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      return new Project(configurationProvider, rootPath);
    }

    private static IEnumerable<FullPath> EnumerateParents(FullPath path) {
      var directory = path.Parent;
      for (var parent = directory; parent != default(FullPath); parent = parent.Parent) {
        yield return parent;
      }
    }

    public bool ContainsProjectFile(FullPath path) {
      return _fileSystem.FileExists(path.Combine(new RelativePath(ConfigurationFileNames.ProjectFileNameDetection)));
    }
  }
}
