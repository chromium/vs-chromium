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
    private readonly FullPathNameSet<Project> _knownProjectRootDirectories = new FullPathNameSet<Project>();
    private readonly FullPathNameSet<object> _knownNonProjectDirectories = new FullPathNameSet<object>();
    private readonly object _lock = new object();

    public int Priority { get { return 100; } }

    public IProject GetProject(FullPathName filename) {
      var name = filename;
      lock (_lock) {
        // Cache hit?
        var root = _knownProjectRootDirectories
          .Where(x => name.StartsWith(x.Key))
          .OrderByDescending(x => x.Key.FullName.Length)
          .FirstOrDefault();
        if (root.Key != default(FullPathName)) {
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

    public IProject GetProjectFromRootPath(FullPathName projectRootPath) {
      var name = projectRootPath;
      lock (_lock) {
        return _knownProjectRootDirectories.Get(name);
      }
    }

    public void ValidateCache() {
      lock (_lock) {
        _knownProjectRootDirectories.RemoveWhere(x => !x.Key.DirectoryExists);
        _knownProjectRootDirectories.RemoveWhere(x => x.Value.IsOutdated);
        _knownNonProjectDirectories.RemoveWhere(x => !x.Key.DirectoryExists);
      }
    }

    private IProject GetProjectWorker(FullPathName filepath) {
      var directory = filepath.Parent;
      if (directory.DirectoryExists) {
        var projectPath = EnumerateParents(filepath).FirstOrDefault(x => ContainsProjectFile(x));
        if (projectPath != default(FullPathName)) {
          var project = CreateProject(projectPath);
          _knownProjectRootDirectories.Add(projectPath, project);
          return project;
        }
      }

      // No one in the parent chain is a Chromium directory.
      EnumerateParents(filepath).ForAll(x => _knownNonProjectDirectories.Add(x, null));
      return null;
    }

    private Project CreateProject(FullPathName rootPath) {
      var fileWithSections = new FileWithSections(rootPath.Combine(ConfigurationFilenames.ProjectFileNameDetection));
      var configurationProvider = new FileWithSectionConfigurationProvider(fileWithSections);
      return new Project(configurationProvider, rootPath);
    }

    private static IEnumerable<FullPathName> EnumerateParents(FullPathName path) {
      var directory = path.Parent;
      for (var parent = directory; parent != default(FullPathName); parent = parent.Parent) {
        yield return parent;
      }
    }

    public static bool ContainsProjectFile(FullPathName path) {
      return path.Combine(ConfigurationFilenames.ProjectFileNameDetection).FileExists;
    }
  }
}
