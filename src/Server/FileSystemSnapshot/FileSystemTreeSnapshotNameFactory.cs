using System;
using System.Collections.Generic;
using VsChromium.Core.Collections;
using VsChromium.Core.FileNames;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemSnapshot {
  public class FileSystemTreeSnapshotNameFactory : IFileSystemNameFactory {
    // We can use the fast case sentive comparer as this factory is merely a cache
    // for previously re-used names.
    private static readonly IComparer<string> NameComparer = StringComparer.Ordinal;
    private static readonly Func<ProjectRootSnapshot, FullPathName, int> ProjectRootComparer = (x, item) => NameComparer.Compare(x.Directory.DirectoryName.FullPathName.FullName, item.FullName);
    private static readonly Func<DirectorySnapshot, string, int> DirectoryComparer = (x, item) => NameComparer.Compare(x.DirectoryName.RelativePathName.FileName, item);
    private static readonly Func<DirectorySnapshot, DirectoryName, int> DirectoryNameComparer = (x, item) => NameComparer.Compare(x.DirectoryName.RelativePathName.FileName, item.RelativePathName.FileName);
    private static readonly Func<FileName, string, int> FileComparer = (x, item) => NameComparer.Compare(x.RelativePathName.FileName, item);

    private readonly FileSystemTreeSnapshot _snapshot;
    private readonly IFileSystemNameFactory _previous;

    public FileSystemTreeSnapshotNameFactory(FileSystemTreeSnapshot snapshot, IFileSystemNameFactory previous) {
      _snapshot = snapshot;
      _previous = previous;
    }

    public DirectoryName CreateAbsoluteDirectoryName(FullPathName rootPath) {
      var rootdirectory = FindRootDirectory(rootPath);
      if (rootdirectory != null)
        return rootdirectory.DirectoryName;

      return _previous.CreateAbsoluteDirectoryName(rootPath);
    }

    public DirectoryName CreateDirectoryName(DirectoryName parent, string name) {
      var directory = FindDirectory(parent);
      if (directory != null) {
        // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
        int index = SortedArray.BinarySearch(directory.DirectoryEntries, name, DirectoryComparer);
        if (index >= 0) {
          return directory.DirectoryEntries[index].DirectoryName;
        }
      }
      return _previous.CreateDirectoryName(parent, name);
    }

    public FileName CreateFileName(DirectoryName parent, string name) {
      var directory = FindDirectory(parent);
      if (directory != null) {
        // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
        int index = SortedArray.BinarySearch(directory.Files, name, FileComparer);
        if (index >= 0) {
          return directory.Files[index];
        }
      }
      return _previous.CreateFileName(parent, name);
    }

    public DirectorySnapshot FindRootDirectory(FullPathName rootPath) {
      var index = SortedArray.BinarySearch(_snapshot.ProjectRoots, rootPath, ProjectRootComparer);
      if (index >= 0)
        return _snapshot.ProjectRoots[index].Directory;

      return null;
    }

    private DirectorySnapshot FindDirectory(DirectoryName name) {
      if (name.IsAbsoluteName)
        return FindRootDirectory(name.FullPathName);

      var parent = FindDirectory(name.Parent);
      if (parent == null)
        return null;

      // Note: We found the corresponding parent, we just need to check the "simple" name part of the relative name.
      var index = SortedArray.BinarySearch(parent.DirectoryEntries, name, DirectoryNameComparer);
      if (index < 0)
        return null;

      return parent.DirectoryEntries[index];
    }
  }
}