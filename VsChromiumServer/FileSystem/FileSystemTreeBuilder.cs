// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumCore.Linq;
using VsChromiumServer.ProgressTracking;
using VsChromiumServer.Projects;

namespace VsChromiumServer.FileSystem {
  public class FileSystemTreeBuilder {
    private const string _relativePathForRoot = "";
    private static readonly EntryReverseComparer _entryReverseComparerInstance = new EntryReverseComparer();
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    public FileSystemTreeBuilder(
      IProjectDiscovery projectDiscovery,
      IProgressTrackerFactory progressTrackerFactory) {
      _projectDiscovery = projectDiscovery;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public DirectoryEntry ComputeNewRoot(IEnumerable<string> files) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var newRoot = new DirectoryEntry();

        newRoot.Entries = files
          .Select(filename => _projectDiscovery.GetProject(filename))
          .Where(project => project != null)
          .Distinct(new ProjectPathComparer())
          .Select(project => ProcessProject(project, progress))
          .Where(entry => entry != null)
          .OrderBy(entry => entry.Name, SystemPathComparer.Instance.Comparer)
          .Cast<FileSystemEntry>()
          .ToList();

        return newRoot;
      }
    }

    private DirectoryEntry ProcessProject(IProject project, IProgressTracker progress) {
      // List of [string(RelativePath), DirectoryEntry]
      var directories = TraverseFileSystem(project)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(x => {
          var directoryName = x.DirectoryName;
          progress.Step(
            (i, n) =>
            string.Format("Traversing directory: {0}",
                          PathHelpers.PathCombine(project.RootPath, directoryName.RelativeName)));
          var entryName = (directoryName.RelativeName == _relativePathForRoot ? project.RootPath : directoryName.Name);
          var directoryEntry = new DirectoryEntry {
            Name = entryName,
            RelativePathName = directoryName
          };

          var childrenNames = x.ChildrenNames;
          var entries = childrenNames
            .Where(file => project.FileFilter.Include(file.RelativeName))
            .Select(file => new FileEntry {
              Name = file.Name,
              RelativePathName = file
            })
            .OrderBy(fileEntry => fileEntry.Name, SystemPathComparer.Instance.Comparer);
          directoryEntry.Entries.AddRange(entries);

          return new KeyValuePair<string, DirectoryEntry>(directoryName.RelativeName, directoryEntry);
        })
        // We sort entries by file name descending to make sure we process
        // directories bottom up, so that we know it is safe to skip 
        // DirectoryEntry instances where "Entries.Count" == 0.
        .OrderByDescending(x => x.Key, SystemPathComparer.Instance.Comparer)
        .ToList();

      directories
        .ForAll(x => {
          var relativePath = x.Key;
          if (relativePath == _relativePathForRoot)
            return;

          // If current entry has no entries, don't add it to the parent entries list.
          var directoryEntry = x.Value;
          if (directoryEntry.Entries.Count == 0)
            return;

          // We attach the entry to the parent directory
          var parentPath = Path.GetDirectoryName(relativePath);
          var parentEntry = FindEntry(directories, parentPath).Value;
          parentEntry.Entries.Add(directoryEntry);
        });

      var result = FindEntry(directories, _relativePathForRoot).Value;
      SortEntries(result);
      return result;
    }

    /// <summary>
    /// Return the list of all directories (and their files) starting at the absolute RelativeName "rootPath".
    /// </summary>
    private IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(IProject project) {
      var stack = new Stack<RelativePathName>();
      stack.Push(new RelativePathName(_relativePathForRoot, _relativePathForRoot));
      while (stack.Count > 0) {
        var head = stack.Pop();
        var isRootFolder = head.RelativeName == _relativePathForRoot;
        if (isRootFolder || project.DirectoryFilter.Include(head.RelativeName)) {
          RelativePathName[] childDirectories;
          RelativePathName[] childFiles;
          head.GetFileSystemEntries(project.RootPath, out childDirectories, out childFiles);
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childDirectories.Length; i++) {
            stack.Push(childDirectories[i]);
          }
          yield return new TraversedDirectoryEntry(head, childFiles);
        }
      }
    }

    private static KeyValuePair<string, DirectoryEntry> FindEntry(
      List<KeyValuePair<string, DirectoryEntry>> directories,
      string relativePath) {
      var item = new KeyValuePair<string, DirectoryEntry>(relativePath, null);
      var result = directories.BinarySearch(item, _entryReverseComparerInstance);
      return directories[result];
    }

    private void SortEntries(DirectoryEntry entry) {
      if (entry == null)
        return;

      entry.Entries.Sort((x, y) => {
        if (x.GetType() == y.GetType())
          return SystemPathComparer.Instance.Comparer.Compare(x.Name, y.Name);

        if (x is DirectoryEntry)
          return -1;

        return 1;
      });
      entry.Entries.OfType<DirectoryEntry>().ForAll(x => SortEntries(x));
    }

    private class EntryReverseComparer : IComparer<KeyValuePair<string, DirectoryEntry>> {
      public int Compare(KeyValuePair<string, DirectoryEntry> x, KeyValuePair<string, DirectoryEntry> y) {
        // Note: x and y are swapped below!
        return SystemPathComparer.Instance.Comparer.Compare(y.Key, x.Key);
      }
    }

    private struct TraversedDirectoryEntry {
      private readonly RelativePathName[] _childrenNames;
      private readonly RelativePathName _directoryName;

      public TraversedDirectoryEntry(RelativePathName directoryName, RelativePathName[] childrenNames) {
        _directoryName = directoryName;
        _childrenNames = childrenNames;
      }

      public RelativePathName DirectoryName { get { return _directoryName; } }

      public RelativePathName[] ChildrenNames { get { return _childrenNames; } }
    }
  }
}
