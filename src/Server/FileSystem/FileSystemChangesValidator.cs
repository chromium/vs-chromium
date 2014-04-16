// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.FileNames;
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
      var unfilteredChanges = changes
        .Where(x => !PathIsExcluded(x.Path))
        .ToList();

      Logger.Log("DirectoryChangeWatcherOnPathsChanged: {0:n0} items left out of {1:n0} after filtering.",
                 unfilteredChanges.Count, changes.Count);
      // Too verbose
      //unfilteredChanges.ForAll(change => Logger.Log("DirectoryChangeWatcherOnPathsChanged({0}).", change));

      if (unfilteredChanges.Any()) {
        // If the only changes we see are file modification, don't recompute the graph, just 
        // raise a "files changes event".
        if (unfilteredChanges.All(change => change.Kind == PathChangeKind.Changed)) {
          Logger.Log(
            "All changes are file modifications, so we don't update the FileSystemTree, but we notify our consumers.");
          var fileNames = unfilteredChanges.Select(change => PathToFileName(change.Path)).Where(name => name != null);
          return new FileSystemValidationResult {
            ChangeFiles = fileNames.ToList()
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

    private bool PathIsExcluded(string path) {
      var project = _projectDiscovery.GetProject(new FullPathName(path));
      if (project == null)
        return true;

      var rootPath = project.RootPath;

      // If path is root itself, it is never excluded.
      if (rootPath.Length == path.Length)
        return false;

      var rootLength = rootPath.Length + 1; // Move past '\\' character.
      if (rootPath.Last() == Path.DirectorySeparatorChar)
        rootLength--;

      var relativePath = path.Substring(rootLength);
      var items = relativePath.Split(new char[] {
        Path.DirectorySeparatorChar
      });
      var pathToItem = "";
      foreach (var item in items) {
        var relativePathToItem = Path.Combine(pathToItem, item);

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

    private FileName PathToFileName(string path) {
      var rootPath = _projectDiscovery.GetProjectPath(new FullPathName(path));
      if (rootPath == null)
        return null;

      var rootLength = rootPath.Length + 1;
      if (rootPath.Last() == Path.DirectorySeparatorChar)
        rootLength--;

      var directoryName = _fileSystemNameFactory.CombineDirectoryNames(_fileSystemNameFactory.Root, rootPath);
      var relativePath = path.Substring(rootLength);
      var items = relativePath.Split(new char[] {
        Path.DirectorySeparatorChar
      });
      foreach (var item in items) {
        if (item == items.Last())
          return _fileSystemNameFactory.CombineFileName(directoryName, item);

        directoryName = _fileSystemNameFactory.CombineDirectoryNames(directoryName, item);
      }
      return null;
    }
  }
}
