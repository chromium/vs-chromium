// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public static class FileSystemNameFactoryExtensions {
    /// <summary>
    /// Return the <see cref="ProjectFileName"/> instance of the full path <paramref name="path"/>.
    /// Returns the default value if <paramref name="path"/> is invalid or not part of a project.
    /// </summary>
    public static ProjectFileName CreateProjectFileFromFullPath(this IFileSystemNameFactory fileSystemNameFactory,
      IProjectDiscovery projectDiscovery, FullPath path) {
      var project = projectDiscovery.GetProjectFromAnyPath(path);
      if (project == null)
        return default(ProjectFileName);

      var split = PathHelpers.SplitPrefix(path.Value, project.RootPath.Value);
      return CreateProjectFileNameFromRelativePath(fileSystemNameFactory, project, new RelativePath(split.Suffix));
    }

    /// <summary>
    /// Return the <see cref="ProjectFileName"/> instance of the project file <paramref name="relativePath"/>.
    /// Returns the default value if <paramref name="relativePath"/> is invalid or not part of a project.
    /// </summary>
    public static ProjectFileName CreateProjectFileNameFromRelativePath(
      this IFileSystemNameFactory fileSystemNameFactory, IProject project, RelativePath relativePath) {
      return CreateProjectFileNameFromRelativePath(fileSystemNameFactory, project, relativePath.Value);
    }

    /// <summary>
    /// Return the <see cref="ProjectFileName"/> instance of the project file <paramref name="relativePath"/>.
    /// Returns the default value if <paramref name="relativePath"/> is invalid or not part of a project.
    /// </summary>
    public static ProjectFileName CreateProjectFileNameFromRelativePath(
      this IFileSystemNameFactory fileSystemNameFactory, IProject project, string relativePath) {
      if (project == null) {
        throw new ArgumentNullException();
      }
      if (string.IsNullOrEmpty(relativePath)) {
        return default(ProjectFileName);
      }

      var directoryName = fileSystemNameFactory.CreateAbsoluteDirectoryName(project.RootPath);
      var names = PathHelpers.SplitPath(relativePath).ToList();

      foreach (var name in names) {
        if (name == names.Last()) {
          return new ProjectFileName(project, fileSystemNameFactory.CreateFileName(directoryName, name));
        }

        directoryName = fileSystemNameFactory.CreateDirectoryName(directoryName, name);
      }

      throw Invariants.Fail("Unreachable code");
    }
  }
}
