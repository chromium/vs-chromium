// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemTree {
  [Export(typeof(IFileSystemTreeBuilder))]
  public class FileSystemTreeBuilder : IFileSystemTreeBuilder {
    private static readonly EntryReverseComparer _entryReverseComparerInstance = new EntryReverseComparer();
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;

    [ImportingConstructor]
    public FileSystemTreeBuilder(
      IProjectDiscovery projectDiscovery,
      IProgressTrackerFactory progressTrackerFactory,
      IFileSystemNameFactory fileSystemNameFactory) {
      _projectDiscovery = projectDiscovery;
      _progressTrackerFactory = progressTrackerFactory;
      _fileSystemNameFactory = fileSystemNameFactory;
    }

    public FileSystemTreeInternal ComputeTree(IEnumerable<FullPathName> filenames) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var newRoot = new DirectoryEntryInternal(
          _fileSystemNameFactory.Root,
          filenames
            .Select(filename => _projectDiscovery.GetProject(filename))
            .Where(project => project != null)
            .Distinct(new ProjectPathComparer())
            .Select(project => ProcessProject(project, progress))
            .Where(entry => entry != null)
            .OrderBy(entry => entry.FileSystemName)
            .Cast<FileSystemEntryInternal>()
            .ToReadOnlyCollection()
          );

        return new FileSystemTreeInternal(newRoot);
      }
    }

    private DirectoryEntryInternal ProcessProject(IProject project, IProgressTracker progress) {
      // List of [DirectoryName, DirectoryEntryInternal]
      var projectPath = _fileSystemNameFactory.CombineDirectoryNames(_fileSystemNameFactory.Root, project.RootPath);
      var directories = TraverseFileSystem(project, projectPath)
        //.AsParallel()
        //.WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(traversedDirectoryEntry => {
          var directoryName = traversedDirectoryEntry.DirectoryName;
          progress.Step(
            (i, n) =>
            string.Format("Traversing directory: {0}",
                          PathHelpers.PathCombine(project.RootPath, directoryName.RelativePathName.RelativeName)));
          var entries = traversedDirectoryEntry.ChildrenNames
            .Where(childDirectory => project.FileFilter.Include(childDirectory.RelativeName))
            .Select(childDirectory => new FileEntryInternal(_fileSystemNameFactory.CreateFileName(directoryName, childDirectory)))
            .OfType<FileSystemEntryInternal>()
            .ToReadOnlyCollection();

          var directoryEntry = new DirectoryEntryInternal(directoryName, entries);
          return new KeyValuePair<DirectoryName, DirectoryEntryInternal>(directoryName, directoryEntry);
        })
        // We sort entries by file name descending to make sure we process
        // directories bottom up, so that we know it is safe to skip 
        // DirectoryEntry instances where "Entries.Count" == 0.
        .OrderByDescending(x => x.Key)
        .ToList();

      // Connect directory entries to their parent
      directories
        .ForAll(x => {
          var directoryEntry = x.Value;

          // Root directory of project has no parent.
          Debug.Assert(!directoryEntry.Name.IsRoot);
          if (directoryEntry.Name.IsAbsoluteName) {
            Debug.Assert(directoryEntry.Name.Equals(projectPath));
            return;
          }

          // If current entry has no entries, don't add it to the parent entries list.
          if (directoryEntry.Entries.Count == 0)
            return;

          // We attach the entry to the parent directory
          var parentName = directoryEntry.Name.Parent;
          var parentEntry = FindEntry(directories, parentName).Value;
          parentEntry.Entries.Add(directoryEntry);
        });

      var result = FindEntry(directories, projectPath).Value;
      SortEntries(result);
      return result;
    }

    /// <summary>
    /// Enumerate directories and files under the project path of |projet|.
    /// </summary>
    private IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(IProject project, DirectoryName projectPath) {
      Debug.Assert(!projectPath.IsRoot);
      Debug.Assert(projectPath.IsAbsoluteName);
      var stack = new Stack<DirectoryName>();
      stack.Push(projectPath);
      while (stack.Count > 0) {
        var head = stack.Pop();
        if (head.IsAbsoluteName || project.DirectoryFilter.Include(head.RelativePathName.RelativeName)) {
          RelativePathName[] childDirectories;
          RelativePathName[] childFiles;
          head.RelativePathName.GetFileSystemEntries(project.RootPath, out childDirectories, out childFiles);
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childDirectories.Length; i++) {
            stack.Push(_fileSystemNameFactory.CreateDirectoryName(head, childDirectories[i]));
          }
          yield return new TraversedDirectoryEntry(head, childFiles);
        }
      }
    }

    private static KeyValuePair<DirectoryName, DirectoryEntryInternal> FindEntry(List<KeyValuePair<DirectoryName, DirectoryEntryInternal>> directories, DirectoryName directoryName) {
      var item = new KeyValuePair<DirectoryName, DirectoryEntryInternal>(directoryName, null);
      var result = directories.BinarySearch(item, _entryReverseComparerInstance);
      Debug.Assert(result >= 0, "Bug: The directory name entry should be present in the list of directories.");
      return directories[result];
    }

    private void SortEntries(DirectoryEntryInternal entry) {
      if (entry == null)
        return;

      entry.Entries.Sort((x, y) => {
        if (x.GetType() == y.GetType())
          return x.FileSystemName.CompareTo(y.FileSystemName);

        if (x is DirectoryEntryInternal)
          return -1;

        return 1;
      });
      entry.Entries.TrimExcess();
      entry.Entries.OfType<DirectoryEntryInternal>().ForAll(x => SortEntries(x));
    }

    private class EntryReverseComparer : IComparer<KeyValuePair<DirectoryName, DirectoryEntryInternal>> {
      public int Compare(KeyValuePair<DirectoryName, DirectoryEntryInternal> x, KeyValuePair<DirectoryName, DirectoryEntryInternal> y) {
        return -x.Key.CompareTo(y.Key);
      }
    }

    private struct TraversedDirectoryEntry {
      private readonly DirectoryName _directoryName;
      private readonly RelativePathName[] _childrenNames;

      public TraversedDirectoryEntry(DirectoryName directoryName, RelativePathName[] childrenNames) {
        _directoryName = directoryName;
        _childrenNames = childrenNames;
      }

      public DirectoryName DirectoryName { get { return _directoryName; } }
      public RelativePathName[] ChildrenNames { get { return _childrenNames; } }
    }
  }
}
