// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  [Export(typeof(IFileSystemSnapshotBuilder))]
  public class FileSystemSnapshotBuilder : IFileSystemSnapshotBuilder {
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileSystemSnapshotBuilder(
      IProjectDiscovery projectDiscovery,
      IProgressTrackerFactory progressTrackerFactory) {
      _projectDiscovery = projectDiscovery;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public FileSystemTreeSnapshot Compute(IFileSystemNameFactory fileNameFactory, IEnumerable<FullPathName> filenames, int version) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var projectRoots =
          filenames
            .Select(filename => _projectDiscovery.GetProject(filename))
            .Where(project => project != null)
            .Distinct(new ProjectPathComparer())
            .Select(project => new ProjectRootSnapshot(project, ProcessProject(fileNameFactory, project, progress)))
            .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
            .ToReadOnlyCollection();

        return new FileSystemTreeSnapshot(version, projectRoots);
      }
    }

    private static List<TValue> GetOrCreateList<TKey, TValue>(IDictionary<TKey, List<TValue>> dictionary, TKey key) {
      List<TValue> children;
      if (dictionary.TryGetValue(key, out children))
        return children;

      children = new List<TValue>();
      dictionary[key] = children;
      return children;
    }

    private static IEnumerable<TValue> GetOrEmptyList<TKey, TValue>(IDictionary<TKey, List<TValue>> dictionary, TKey key) {
      List<TValue> children;
      if (dictionary.TryGetValue(key, out children))
        return children;

      return Enumerable.Empty<TValue>();
    }

    private DirectorySnapshot ProcessProject(IFileSystemNameFactory fileNameFactory, IProject project, IProgressTracker progress) {
      var projectPath = fileNameFactory.CreateAbsoluteDirectoryName(project.RootPath);

      var ssw = new ScopedStopWatch();
      // Create list of pairs (DirectoryName, List[FileNames])
      var directoriesWithFiles = TraverseFileSystem(fileNameFactory, project, projectPath)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(traversedDirectoryEntry => {
          var directoryName = traversedDirectoryEntry.DirectoryName;
          if (progress.Step()) {
            progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}", project.RootPath.FullName, directoryName.RelativePathName.RelativeName));
          }
          var entries = traversedDirectoryEntry.ChildrenNames
            .Where(childFilename => project.FileFilter.Include(childFilename.RelativePathName.RelativeName))
            .OrderBy(x => x.RelativePathName)
            .ToReadOnlyCollection();

          return KeyValuePair.Create(directoryName, entries);
        })
        .ToList();
      ssw.Step(sw => Logger.Log("Done traversing file system in {0:n0} msec.", sw.ElapsedMilliseconds));

      // We sort entries by directory name *descending* to make sure we process
      // directories bottom up, so that we know
      // 1) it is safe to skip DirectoryEntry instances where "Entries.Count" == 0,
      // 2) we create instances of child directories before their parent.
      directoriesWithFiles.Sort((x, y) => -x.Key.RelativePathName.CompareTo(y.Key.RelativePathName));
      ssw.Step(sw => Logger.Log("Done sorting list of directories in {0:n0} msec.", sw.ElapsedMilliseconds));

      // Build map from parent directory -> list of child directories
      var directoriesToChildDirectories = new Dictionary<DirectoryName, List<DirectoryName>>();
      directoriesWithFiles.ForAll(x => {
        var directoryName = x.Key;

        // Ignore root project directory name
        if (directoryName.IsAbsoluteName)
          return;

        GetOrCreateList(directoriesToChildDirectories, directoryName.Parent).Add(directoryName);
      });
      ssw.Step(sw => Logger.Log("Done creating children directories in {0:n0} msec.", sw.ElapsedMilliseconds));

      // Build directory snapshots for each directory entry, using an
      // intermediate map to enable connecting snapshots to their parent.
      var directoriesToSnapshot = new Dictionary<DirectoryName, DirectorySnapshot>();
      var directorySnapshots = directoriesWithFiles.Select(entry => {
        var directoryName = entry.Key;
        var childFilenames = entry.Value;

        var childDirectories = GetOrEmptyList(directoriesToChildDirectories, directoryName)
          .Select(x => directoriesToSnapshot[x])
          .OrderBy(x => x.DirectoryName.RelativePathName)
          .ToReadOnlyCollection();

        // TODO(rpaquay): Not clear the lines below are a perf win, even though
        // they do not hurt correctness.
        // Remove children since we processed them
        //GetOrEmptyList(directoriesToChildDirectories, directoryName)
        //  .ForAll(x => directoriesToSnapshot.Remove(x));

        var result = new DirectorySnapshot(directoryName, childDirectories, childFilenames);
        directoriesToSnapshot.Add(directoryName, result);
        return result;
      })
      .ToList();
      ssw.Step(sw => Logger.Log("Done creating directory snapshots in {0:n0} msec.", sw.ElapsedMilliseconds));

      // Since we sort directories by name descending, the last entry is always the
      // entry correcsponding to the project root.
      Debug.Assert(directorySnapshots.Count >= 1);
      Debug.Assert(directorySnapshots.Last().DirectoryName.Equals(projectPath));
      return directorySnapshots.Last();
    }

    /// <summary>
    /// Enumerate directories and files under the project path of |projet|.
    /// </summary>
    private IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(IFileSystemNameFactory fileNameFactory, IProject project, DirectoryName projectPath) {
      Debug.Assert(projectPath.IsAbsoluteName);
      var stack = new Stack<DirectoryName>();
      stack.Push(projectPath);
      while (stack.Count > 0) {
        var head = stack.Pop();
        if (head.IsAbsoluteName || project.DirectoryFilter.Include(head.RelativePathName.RelativeName)) {
          IList<string> childDirectories;
          IList<string> childFiles;
          RelativePathNameExtensions.GetFileSystemEntries(project.RootPath, head.RelativePathName, out childDirectories, out childFiles);
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childDirectories.Count; i++) {
            stack.Push(fileNameFactory.CreateDirectoryName(head, childDirectories[i]));
          }
          // Note: Use "for" loop to avoid memory allocations.
          var childFilenames = new FileName[childFiles.Count];
          for (var i = 0; i < childFiles.Count; i++) {
            childFilenames[i] = fileNameFactory.CreateFileName(head, childFiles[i]);
          }
          yield return new TraversedDirectoryEntry(head, childFilenames);
        }
      }
    }

    private struct TraversedDirectoryEntry {
      private readonly DirectoryName _directoryName;
      private readonly IList<FileName> _childrenNames;

      public TraversedDirectoryEntry(DirectoryName directoryName, IList<FileName> childrenNames) {
        _directoryName = directoryName;
        _childrenNames = childrenNames;
      }

      public DirectoryName DirectoryName { get { return _directoryName; } }
      public IList<FileName> ChildrenNames { get { return _childrenNames; } }
    }
  }
}
