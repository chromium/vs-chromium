// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.FileNames;
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
    public IProject GetProject(FullPath filename) {
      var name = filename;
      lock (_lock) {
        // Cache hit?
        var root = _knownProjectRootDirectories
          .Where(x => name.StartsWith(x.Key))
          .OrderByDescending(x => x.Key.FullName.Length)
          .FirstOrDefault();
        if (root.Key != default(FullPath)) {
          return root.Value;
        }

        // Negative cache hit?
        if (_knownNonProjectDirectories.Contains(name.Parent)) {
          return null;
        }

        // Nope: compute all the way...
        return GetProjectWorker(name);
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
        _knownProjectRootDirectories.RemoveWhere(x => !_fileSystem.DirectoryExists(x.Key));
        _knownProjectRootDirectories.RemoveWhere(x => x.Value.IsOutdated);
        _knownNonProjectDirectories.RemoveWhere(x => !_fileSystem.DirectoryExists(x.Key));
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

    private Project CreateProject(FullPath rootPath) {
      var fileWithSections = new FileWithSections(rootPath.Combine(ConfigurationFilenames.ProjectFileNameDetection));
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      return new Project(configurationProvider, rootPath);
    }

    private static IEnumerable<FullPath> EnumerateParents(FullPath path) {
      var directory = path.Parent;
      for (var parent = directory; parent != default(FullPath); parent = parent.Parent) {
        yield return parent;
      }
    }

    public static bool ContainsProjectFile(FullPath path) {
      return path.Combine(ConfigurationFilenames.ProjectFileNameDetection).FileExists;
    }
  }
}
