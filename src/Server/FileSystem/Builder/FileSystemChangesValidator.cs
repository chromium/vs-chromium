// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
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

      if (Logger.Info) {
        Logger.LogInfo("ProcessPathsChangedEvent: {0:n0} items left out of {1:n0} after filtering (showing max 5 below).",
          filteredChanges.Count, changes.Count);
        filteredChanges
          .Take(5)
          .ForAll(x =>
            Logger.LogInfo("  Path changed: \"{0}\", kind={1}", x.Path, x.Kind));
      }

      if (filteredChanges.Count == 0) {
        Logger.LogInfo("All changes have been filtered out.");

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

      if (filteredChanges.All(x => x.Kind == PathChangeKind.Changed)) {
        Logger.LogInfo("All file change events are file modifications.");

        var fileNames = filteredChanges
          .Select(change => GetProjectFileName(change.BasePath))
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

      var project = _projectDiscovery.GetProject(change.BasePath);
      if (project == null)
        return true;

      // Split relative part into list of name components.
      var segments = change.RelativePath.Value.Split(Path.DirectorySeparatorChar);

      // Check each relative path from root path to full path.
      var parentPath = new RelativePath();
      foreach (var segment in segments) {
        var segmentPath = parentPath.CreateChild(segment);

        var exclude = RelativePathShouldBeIgnored(project, change, segmentPath);
        if (exclude)
          return true;

        parentPath = segmentPath;
      }
      return false;
    }

    private bool RelativePathShouldBeIgnored(IProject project, PathChangeEntry change, RelativePath segmentPath) {
      // For the last component, we don't know if it is a file or directory.
      if (segmentPath.Equals(change.RelativePath)) {
        // Try to avoid Disk I/O if the path should be excluded
        var fileShouldBeIgnored = !project.FileFilter.Include(segmentPath);
        var directoryShouldBeIgnored = !project.DirectoryFilter.Include(segmentPath);
        if (fileShouldBeIgnored && directoryShouldBeIgnored) {
          return true;
        }

        // Check depending on the change kind.
        if (change.Kind == PathChangeKind.Deleted) {
          return false;
        }

        var info = _fileSystem.GetFileInfoSnapshot(change.Path);
        if (info.IsFile) {
          return fileShouldBeIgnored;
        }

        if (info.IsDirectory) {
          return directoryShouldBeIgnored;
        }

        // We don't know... Be conservative.
        return false;
      }

      return !project.DirectoryFilter.Include(segmentPath);
    }

    private ProjectFileName GetProjectFileName(FullPath path) {
      return FileSystemNameFactoryExtensions.GetProjectFileName(_fileSystemNameFactory, _projectDiscovery, path);
    }
  }
}
