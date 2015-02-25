// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public static class FileSystemNameFactoryExtensions {
    public static DirectoryEntry ToFlatSearchResult(IFileSystemNameFactory fileSystemNameFactory, IEnumerable<FileSystemName> names) {
      Func<FileSystemName, FileSystemName> fileNameMapper = x => x;
      Func<FileSystemName, FileSystemEntryData> dataMapper = x => null;
      return ToFlatSearchResult(fileSystemNameFactory, names, fileNameMapper, dataMapper);
    }

    public static DirectoryEntry ToFlatSearchResult<TSource>(IFileSystemNameFactory fileSystemNameFactory,
                                                             IEnumerable<TSource> source,
                                                             Func<TSource, FileSystemName> fileNameMapper,
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
                                                                     Func<TSource, FileSystemName> fileNameMapper,
                                                                     Func<TSource, FileSystemEntryData> dataMapper) {
      return grouping
        .Where(x => !fileNameMapper(x).IsAbsoluteName)
        .OrderBy(x => fileNameMapper(x).RelativePath)
        .Select(x => CreateFileSystemEntry(x, fileNameMapper, dataMapper));
    }

    private static FileSystemEntry CreateFileSystemEntry<T>(T item, Func<T, FileSystemName> fileNameMapper, Func<T, FileSystemEntryData> dataMapper) {
      var name = fileNameMapper(item);
      var data = dataMapper(item);
      if (name is FileName)
        return new FileEntry {
          Name = name.RelativePath.Value,
          Data = data
        };
      else
        return new DirectoryEntry {
          Name = name.RelativePath.Value,
          Data = data
        };
    }

    /// <summary>
    /// Returns the "project root" part of a <see cref="FileSystemName"/>. This
    /// function assumes "project root" is the absolute directory of <paramref
    /// name="name"/>.
    /// </summary>
    private static DirectoryName GetProjectRoot(FileSystemName name) {
      if (name == null)
        throw new ArgumentNullException("name");

      if (name.IsAbsoluteName)
        return (DirectoryName)name;

      return GetProjectRoot(name.Parent);
    }

    /// <summary>
    /// Return the |FileName| instance corresponding to the full path |path|.
    /// Returns |null| if |path| is invalid or not part of a project.
    /// </summary>
    public static ProjectFileName GetProjectFileName(IFileSystemNameFactory fileSystemNameFactory, IProjectDiscovery projectDiscovery, FullPath path) {
      var project = projectDiscovery.GetProject(path);
      if (project == null)
        return default(ProjectFileName);

      var split = PathHelpers.SplitPrefix(path.Value, project.RootPath.Value);
      var relativePath = split.Suffix;

      var directoryName = fileSystemNameFactory.CreateAbsoluteDirectoryName(project.RootPath);
      var names = relativePath.Split(Path.DirectorySeparatorChar);

      foreach (var name in names) {
        if (name == names.Last())
          return new ProjectFileName(project, fileSystemNameFactory.CreateFileName(directoryName, name));

        directoryName = fileSystemNameFactory.CreateDirectoryName(directoryName, name);
      }
      return default(ProjectFileName);
    }
  }
}
