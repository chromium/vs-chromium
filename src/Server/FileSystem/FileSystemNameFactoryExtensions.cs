// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Core.Logging;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public static class FileSystemNameFactoryExtensions {
    public static DirectoryEntry ToFlatSearchResult(IFileSystemNameFactory fileSystemNameFactory, IEnumerable<FileName> names) {
      Func<FileName, FileName> fileNameMapper = x => x;
      Func<FileName, FileSystemEntryData> dataMapper = x => null;
      return ToFlatSearchResult(fileSystemNameFactory, names, fileNameMapper, dataMapper);
    }

    public static DirectoryEntry ToFlatSearchResult<TSource>(IFileSystemNameFactory fileSystemNameFactory,
      IEnumerable<TSource> source,
      Func<TSource, FileName> fileNameMapper,
      Func<TSource, FileSystemEntryData> dataMapper) {
      var sw = Stopwatch.StartNew();
      // Group by root directory (typically one)
      var groups = source
        .GroupBy(x => GetProjectRoot(fileNameMapper(x)))
        .OrderBy(g => g.Key)
        .Select(group => new DirectoryEntry {
          Name = group.Key.FullPath.Value,
          Entries = CreateGroup(group, fileNameMapper, dataMapper).ToList()
        });

      // Return entries by group
      var result = new DirectoryEntry() {
        Entries = groups.Cast<FileSystemEntry>().ToList()
      };
      sw.Stop();
      //Logger.LogInfo("ToFlatSearchResult created {0} groups, first group contains {1:n0} elements in {2:n0} msec.",
      //  result.Entries.Count, (result.Entries.Count >= 1 ? ((DirectoryEntry)result.Entries[0]).Entries.Count : -1), sw.ElapsedMilliseconds);
      return result;
    }

    private static IEnumerable<FileSystemEntry> CreateGroup<TSource>(IEnumerable<TSource> grouping,
      Func<TSource, FileName> fileNameMapper,
      Func<TSource, FileSystemEntryData> dataMapper) {
      return grouping
        //.Where(x => !fileNameMapper(x).IsAbsoluteName)
        .OrderBy(x => fileNameMapper(x).RelativePath)
        .Select(x => CreateFileSystemEntry(x, fileNameMapper, dataMapper));
    }

    private static FileSystemEntry CreateFileSystemEntry<T>(T item, Func<T, FileName> fileNameMapper, Func<T, FileSystemEntryData> dataMapper) {
      var name = fileNameMapper(item);
      var data = dataMapper(item);
      //if (name is FileName)
        return new FileEntry {
          Name = name.RelativePath.Value,
          Data = data
        };
      //else
      //  return new DirectoryEntry {
      //    Name = name.RelativePath.Value,
      //    Data = data
      //  };
    }

    /// <summary>
    /// Returns the "project root" part of a <see cref="DirectoryName"/>. This
    /// function assumes "project root" is the absolute directory of <paramref
    /// name="name"/>.
    /// </summary>
    private static DirectoryName GetProjectRoot(DirectoryName name) {
      if (name == null)
        throw new ArgumentNullException("name");

      if (name.IsAbsoluteName)
        return (DirectoryName)name;

      return GetProjectRoot(name.Parent);
    }

    /// <summary>
    /// Returns the "project root" part of a <see cref="DirectoryName"/>. This
    /// function assumes "project root" is the absolute directory of <paramref
    /// name="name"/>.
    /// </summary>
    private static DirectoryName GetProjectRoot(FileName name) {
      return GetProjectRoot(name.Parent);
    }

    /// <summary>
    /// Return the <see cref="ProjectFileName"/> instance of the full path <paramref name="path"/>.
    /// Returns the default value if <paramref name="path"/> is invalid or not part of a project.
    /// </summary>
    public static ProjectFileName CreateProjectFileFromFullPath(this IFileSystemNameFactory fileSystemNameFactory,
      IProjectDiscovery projectDiscovery, FullPath path) {
      var project = projectDiscovery.GetProject(path);
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

      Invariants.Assert(false, "Unreachable code");
      throw new InvalidOperationException();
    }
  }
}