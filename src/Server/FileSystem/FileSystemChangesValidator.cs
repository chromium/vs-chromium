// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class FileSystemChangesValidator {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IProjectDiscovery _projectDiscovery;

    public FileSystemChangesValidator(
      IFileSystemNameFactory fileSystemNameFactory,
      IProjectDiscovery projectDiscovery) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _projectDiscovery = projectDiscovery;
    }

    public FileSystemValidationResult ProcessPathsChangedEvent(IList<PathChangeEntry> changes) {
      // Skip files from filtered out directories
      var filteredChanges = changes
        .Where(x => !PathIsExcluded(x.Path))
        .ToList();

      Logger.Log("ProcessPathsChangedEvent: {0:n0} items left out of {1:n0} after filtering (showing max 5 below).",
                 filteredChanges.Count, changes.Count);
      filteredChanges.Take(5).ForAll(x => 
        Logger.Log("  Path changed: \"{0}\", kind={1}", x.Path, x.Kind));

      if (filteredChanges.Any()) {
        // If the only changes we see are file modification, don't recompute the graph, just 
        // raise a "files changes event". Note that we also watch for any "special" filename.
        bool isLowImpactChange =
          filteredChanges.All(change => change.Kind == PathChangeKind.Changed) &&
          filteredChanges.All(change => !SystemPathComparer.Instance.StringComparer.Equals(change.Path.FileName, ConfigurationFileNames.ProjectFileNameDetection));
        if (isLowImpactChange) {
          Logger.Log(
            "All changes are file modifications, so we don't update the FileSystemTree, but we notify our consumers.");
          var fileNames = filteredChanges.Select(change => GetProjectFileName(change.Path)).Where(name => name != null);
          return new FileSystemValidationResult {
            ChangedFiles = fileNames.ToList()
          };
        } else {
          // TODO(rpaquay): Could we be smarter here?
          Logger.Log(
            "Some changes are *not* file modifications: Use hammer approach and update the whole FileSystemTree.");
          return new FileSystemValidationResult {
            RecomputeGraph = true
          };
        }
      }

      return new FileSystemValidationResult();
    }

    private bool PathIsExcluded(FullPath path) {
      var project = _projectDiscovery.GetProject(path);
      if (project == null)
        return true;

      var rootPath = project.RootPath;

      // If path is root itself, it is never excluded.
      if (rootPath.Value.Length == path.Value.Length)
        return false;

      var rootLength = rootPath.Value.Length + 1; // Move past '\\' character.
      if (rootPath.Value.Last() == Path.DirectorySeparatorChar)
        rootLength--;

      var relativePath = path.Value.Substring(rootLength);
      var items = relativePath.Split(new char[] {
        Path.DirectorySeparatorChar
      });
      var pathToItem = new RelativePath();
      foreach (var item in items) {
        var relativePathToItem = pathToItem.CreateChild(item);

        if (!project.DirectoryFilter.Include(relativePathToItem))
          return true;

        // For the last component, we don't know if it is a file or directory.
        // Be conservative and try both.
        if (item == items.Last()) {
          if (!project.FileFilter.Include(relativePathToItem))
            return true;
        }

        pathToItem = relativePathToItem;
      }
      return false;
    }

    private Tuple<IProject, FileName> GetProjectFileName(FullPath path) {
      return FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
    }
  }
}
