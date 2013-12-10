// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystemNames;
using VsChromiumServer.Projects;

namespace VsChromiumServer.FileSystem {
  public static class FileSystemNameFactoryExtensions {
    public static DirectoryEntry ToFlatSearchResult(
      this IFileSystemNameFactory fileSystemNameFactory,
      IEnumerable<FileSystemName> names) {
      Func<FileSystemName, FileSystemName> fileNameMapper = x => x;
      Func<FileSystemName, FileSystemEntryData> dataMapper = x => null;
      return ToFlatSearchResult(fileSystemNameFactory, names, fileNameMapper, dataMapper);
    }

    public static DirectoryEntry ToFlatSearchResult<TSource>(
      this IFileSystemNameFactory fileSystemNameFactory,
      IEnumerable<TSource> source,
      Func<TSource, FileSystemName> fileNameMapper,
      Func<TSource, FileSystemEntryData> dataMapper) {
      var sw = Stopwatch.StartNew();
      // Group by root directory (typically one)
      var groups = source
        .GroupBy(x => GetProjectRoot(fileNameMapper(x)))
        .OrderBy(g => g.Key)
        .Select(group => new DirectoryEntry {
          Name = group.Key.Name,
          Entries = CreateGroup(group, fileNameMapper, dataMapper).ToList()
        });

      // Return entries by group
      var result = new DirectoryEntry() {
        Entries = groups.Cast<FileSystemEntry>().ToList()
      };
      sw.Stop();
      //Logger.Log("ToFlatSearchResult created {0} groups, first group contains {1:n0} elements in {2:n0} msec.",
      //  result.Entries.Count, (result.Entries.Count >= 1 ? ((DirectoryEntry)result.Entries[0]).Entries.Count : -1), sw.ElapsedMilliseconds);
      return result;
    }

    private static IEnumerable<FileSystemEntry> CreateGroup<TSource>(
      IGrouping<DirectoryName, TSource> grouping,
      Func<TSource, FileSystemName> fileNameMapper,
      Func<TSource, FileSystemEntryData> dataMapper) {
      var baseName = grouping.Key;
      var relativeNames = new Dictionary<FileSystemName, string>();

      return grouping
        .Select(x => CreateFileSystemEntry(relativeNames, baseName, x, fileNameMapper, dataMapper))
        .Where(x => x.Name != string.Empty) // filter out root node itself!
        .OrderBy(x => x.Name, SystemPathComparer.Instance.Comparer);
    }

    private static FileSystemEntry CreateFileSystemEntry<T>(
      Dictionary<FileSystemName, string> relativeNames,
      DirectoryName baseName,
      T item,
      Func<T, FileSystemName> fileNameMapper,
      Func<T, FileSystemEntryData> dataMapper) {
      var name = fileNameMapper(item);
      var data = dataMapper(item);
      if (name is FileName)
        return new FileEntry {
          Name = GetRelativePath(relativeNames, name, baseName),
          Data = data
        };
      else
        return new DirectoryEntry {
          Name = GetRelativePath(relativeNames, name, baseName),
          Data = data
        };
    }

    /// <summary>
    /// Return a string representing the relative path from "baseName" (usually a "Chromium" root directory name).
    /// "relativeNames" is used to memoize results, as there are many more files that directories.
    /// </summary>
    public static string GetRelativePath(
      Dictionary<FileSystemName, string> relativeNames,
      FileSystemName name,
      DirectoryName baseName) {
      string result;
      if (relativeNames.TryGetValue(name, out result))
        return result;

      if (name.IsRoot)
        return name.Name;

      if (Equals(name, baseName))
        return string.Empty;

      result = Path.Combine(GetRelativePath(relativeNames, name.Parent, baseName), name.Name);
      if (name is DirectoryName) // Only add DirectoryNames, as file names are always unique.
        relativeNames.Add(name, result);
      return result;
    }

    /// <summary>
    /// Note: This function assumes the first non root name is a project root folder.
    /// </summary>
    public static DirectoryName GetProjectRoot(this FileSystemName name) {
      if (name == null || name.IsRoot)
        throw new ArgumentException("Invalid name", "name");

      if (name.Parent.IsRoot)
        return (DirectoryName)name;

      return GetProjectRoot(name.Parent);
    }

    /// <summary>
    /// Note: This function assumes the relative path name of |name| is the actual
    /// relative path from the root of the project.
    /// </summary>
    public static string GetRelativePathFromProjectRoot(this FileSystemName name) {
      if (name == null || name.IsRoot)
        throw new ArgumentException("Invalid name", "name");

      return name.RelativePathName.RelativeName;
    }

    /// <summary>
    /// Return the |FileName| instance corresponding to the full path |path|. Returns |null| if |path| 
    /// is invalid or not part of a project.
    /// </summary>
    public static FileName PathToFileName(this IFileSystemNameFactory fileSystemNameFactory, IProjectDiscovery projectDiscovery, string path) {
      var rootPath = projectDiscovery.GetProjectPath(path);
      if (rootPath == null)
        return null;

      var rootLength = rootPath.Length + 1;
      if (rootPath.Last() == Path.DirectorySeparatorChar)
        rootLength--;

      var directoryName = fileSystemNameFactory.CombineDirectoryNames(fileSystemNameFactory.Root, rootPath);
      var relativePath = path.Substring(rootLength);
      var items = relativePath.Split(new char[] {
        Path.DirectorySeparatorChar
      });
      foreach (var item in items) {
        if (item == items.Last())
          return fileSystemNameFactory.CombineFileName(directoryName, item);

        directoryName = fileSystemNameFactory.CombineDirectoryNames(directoryName, item);
      }
      return null;
    }
  }
}
