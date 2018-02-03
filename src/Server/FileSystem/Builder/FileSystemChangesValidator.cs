// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem.Builder {
  public class FileSystemChangesValidator {
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileSystem _fileSystem;
    private readonly IProjectDiscovery _projectDiscovery;

    public FileSystemChangesValidator(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IProjectDiscovery projectDiscovery) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _projectDiscovery = projectDiscovery;
    }

    public FileSystemValidationResult ProcessPathsChangedEvent(IList<PathChangeEntry> changes) {
      // Skip files from filtered out directories
      var filteredChanges = changes
        .Where(x => !PathChangeShouldBeIgnored(x))
        .ToList();

      if (Logger.IsInfoEnabled) {
        Logger.LogInfo("ProcessPathsChangedEvent: {0:n0} items left out of {1:n0} after filtering (showing max 5 below).",
          filteredChanges.Count, changes.Count);
        filteredChanges
          .Take(5)
          .ForAll(x =>
            Logger.LogInfo("  Path changed: \"{0}\", Pathkind={1}, Changekind={2}", x.Path, x.PathKind, x.ChangeKind));
      }

      if (filteredChanges.Count == 0) {
        //Logger.LogInfo("All changes have been filtered out.");

        return new FileSystemValidationResult {
          Kind = FileSystemValidationResultKind.NoChanges
        };
      }

      if (filteredChanges.Any(x => IsProjectFileChange(x))) {
        Logger.LogInfo("At least one change is a project file.");

        return new FileSystemValidationResult {
          Kind = FileSystemValidationResultKind.UnknownChanges
        };
      }

      if (filteredChanges.All(x => x.ChangeKind == PathChangeKind.Changed)) {
        Logger.LogInfo("All file change events are file modifications.");

        var fileNames = filteredChanges
          .Select(change => CreateProjectFileNameFromChangeEntry(change))
          .Where(name => !name.IsNull);

        return new FileSystemValidationResult {
          Kind = FileSystemValidationResultKind.FileModificationsOnly,
          ModifiedFiles = fileNames.ToList()
        };
      }

      // All kinds of file changes
      Logger.LogInfo("Some file change events are create or delete events.");
      return new FileSystemValidationResult {
        Kind = FileSystemValidationResultKind.VariousFileChanges,
        FileChanges = new FullPathChanges(filteredChanges)
      };
    }

    private static bool IsProjectFileChange(PathChangeEntry change) {
      return 
        SystemPathComparer.Instance.StringComparer.Equals(change.Path.FileName, ConfigurationFileNames.ProjectFileNameObsolete) ||
        SystemPathComparer.Instance.StringComparer.Equals(change.Path.FileName, ConfigurationFileNames.ProjectFileName);
    }

    private bool PathChangeShouldBeIgnored(PathChangeEntry change) {
      // If path is root itself, it is never excluded.
      if (change.RelativePath.IsEmpty)
        return false;

      var project = _projectDiscovery.GetProjectFromRootPath(change.BasePath);
      if (project == null)
        return true;

      // Split relative path into list of name components.
      var names = PathHelpers.SplitPath(change.RelativePath.Value).ToList();

      // Check each relative path from root path to full path.
      var pathToItem = new RelativePath();
      foreach (var item in names) {
        var relativePathToItem = pathToItem.CreateChild(item);

        bool exclude;
        // For the last component, we don't know if it is a file or directory.
        // Check depending on the change kind.
        if (item == names.Last()) {
          // Try to avoid Disk I/O if the path should be excluded
          var fileShouldBeIgnored = !project.FileFilter.Include(relativePathToItem);
          var directoryShouldBeIgnored = !project.DirectoryFilter.Include(relativePathToItem);
          if (fileShouldBeIgnored && directoryShouldBeIgnored) {
            exclude = true;
          }
          else {
            if (change.ChangeKind == PathChangeKind.Deleted) {
              // Note: Not sure why this is the case.
              exclude = false;
            }
            else {
              var info = _fileSystem.GetFileInfoSnapshot(change.Path);
              if (info.IsFile) {
                exclude = fileShouldBeIgnored;
              }
              else if (info.IsDirectory) {
                // For directories, a "Change" event can be ignored, as there is nothing
                // to infer a last write time change to a directory.
                exclude = directoryShouldBeIgnored || change.ChangeKind == PathChangeKind.Changed;
              }
              else {
                // We don't know... Be conservative.
                exclude = false;
              }
            }
          }
        } else {
          exclude = !project.DirectoryFilter.Include(relativePathToItem);
        }

        if (exclude)
          return true;

        pathToItem = relativePathToItem;
      }
      return false;
    }

    private ProjectFileName CreateProjectFileNameFromChangeEntry(PathChangeEntry entry) {
      var project = _projectDiscovery.GetProjectFromRootPath(entry.BasePath);
      if (project == null) {
        return default(ProjectFileName);
      }
      return _fileSystemNameFactory.CreateProjectFileNameFromRelativePath(project, entry.RelativePath);
    }
  }
}
