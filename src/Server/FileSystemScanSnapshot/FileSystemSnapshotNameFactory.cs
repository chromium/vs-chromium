using System;
using System.Collections.Generic;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemScanSnapshot {
  public class FileSystemTreeSnapshotNameFactory : IFileSystemNameFactory {
    // We can use the fast case sentive comparer as this factory is merely a cache
    // for previously re-used names.
    private static readonly IComparer<string> NameComparer = StringComparer.Ordinal;
    private static readonly Func<ProjectRootSnapshot, FullPath, int> ProjectRootComparer = (x, item) => NameComparer.Compare(x.Directory.DirectoryName.FullPath.Value, item.Value);
    private static readonly Func<DirectorySnapshot, string, int> DirectoryComparer = (x, item) => NameComparer.Compare(x.DirectoryName.RelativePath.FileName, item);
    private static readonly Func<DirectorySnapshot, DirectoryName, int> DirectoryNameComparer = (x, item) => NameComparer.Compare(x.DirectoryName.RelativePath.FileName, item.RelativePath.FileName);
    private static readonly Func<FileName, string, int> FileComparer = (x, item) => NameComparer.Compare(x.RelativePath.FileName, item);

    private readonly FileSystemTreeSnapshot _snapshot;
    private readonly IFileSystemNameFactory _previous;

    public FileSystemTreeSnapshotNameFactory(FileSystemTreeSnapshot snapshot, IFileSystemNameFactory previous) {
      _snapshot = snapshot;
      _previous = previous;
    }

    public DirectoryName CreateAbsoluteDirectoryName(FullPath rootPath) {
      var rootdirectory = FindRootDirectory(rootPath);
      if (rootdirectory != null)
        return rootdirectory.DirectoryName;

      return _previous.CreateAbsoluteDirectoryName(rootPath);
    }

    public DirectoryName CreateDirectoryName(DirectoryName parent, string name) {
      var directory = FindDirectory(parent);
      if (directory != null) {
        // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
        int index = SortedArrayHelpers.BinarySearch(directory.ChildDirectories, name, DirectoryComparer);
        if (index >= 0) {
          return directory.ChildDirectories[index].DirectoryName;
        }
      }
      return _previous.CreateDirectoryName(parent, name);
    }

    public FileName CreateFileName(DirectoryName parent, string name) {
      var directory = FindDirectory(parent);
      if (directory != null) {
        // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
        int index = SortedArrayHelpers.BinarySearch(directory.ChildFiles, name, FileComparer);
        if (index >= 0) {
          return directory.ChildFiles[index];
        }
      }
      return _previous.CreateFileName(parent, name);
    }

    public DirectorySnapshot FindRootDirectory(FullPath rootPath) {
      var index = SortedArrayHelpers.BinarySearch(_snapshot.ProjectRoots, rootPath, ProjectRootComparer);
      if (index >= 0)
        return _snapshot.ProjectRoots[index].Directory;

      return null;
    }

    private DirectorySnapshot FindDirectory(DirectoryName name) {
      if (name.IsAbsoluteName)
        return FindRootDirectory(name.FullPath);

      var parent = FindDirectory(name.Parent);
      if (parent == null)
        return null;

      // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
      var index = SortedArrayHelpers.BinarySearch(parent.ChildDirectories, name, DirectoryNameComparer);
      if (index < 0)
        return null;

      return parent.ChildDirectories[index];
    }
  }
}