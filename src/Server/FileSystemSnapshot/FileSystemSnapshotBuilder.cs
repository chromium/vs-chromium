// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Core.Win32.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemSnapshot {
  [Export(typeof(IFileSystemSnapshotBuilder))]
  public class FileSystemSnapshotBuilder : IFileSystemSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IProgressTrackerFactory _progressTrackerFactory;

    [ImportingConstructor]
    public FileSystemSnapshotBuilder(
      IFileSystem fileSystem,
      IProjectDiscovery projectDiscovery,
      IProgressTrackerFactory progressTrackerFactory) {
      _fileSystem = fileSystem;
      _projectDiscovery = projectDiscovery;
      _progressTrackerFactory = progressTrackerFactory;
    }

    public FileSystemTreeSnapshot Compute(
      IFileSystemNameFactory fileNameFactory,
      FileSystemTreeSnapshot oldSnapshot,
      FullPathChanges pathChanges/* may be null */,
      List<FullPath> rootFiles,
      int version) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var projectRoots =
          rootFiles
            .Select(filename => _projectDiscovery.GetProject(filename))
            .Where(project => project != null)
            .Distinct(new ProjectPathComparer())
            .Select(project => {
              var rootSnapshot = ProcessProject(fileNameFactory, oldSnapshot, pathChanges, project, progress);
              return new ProjectRootSnapshot(project, rootSnapshot);
            })
            .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
            .ToReadOnlyCollection();

        return new FileSystemTreeSnapshot(version, projectRoots);
      }
    }

    private DirectorySnapshot ProcessProject(
      IFileSystemNameFactory fileNameFactory,
      FileSystemTreeSnapshot oldSnapshot,
      FullPathChanges pathChanges,
      IProject project,
      IProgressTracker progress) {

      if (pathChanges != null) {
        // If we have a project with the same root in the old snapshot, use that
        // snapshot instead of traversing the file system.
        var oldRoot = oldSnapshot.ProjectRoots
          .FirstOrDefault(x => x.Project.RootPath.Equals(project.RootPath));
        if (oldRoot != null) {
          return ApplyDirectorySnapshotDelta(fileNameFactory, project, progress, oldRoot.Directory, pathChanges);
        }
      }

      var projectPath = fileNameFactory.CreateAbsoluteDirectoryName(project.RootPath);
      return CreateDirectorySnapshot(fileNameFactory, project, progress, projectPath, false);
    }

    private DirectorySnapshot CreateDirectorySnapshot(
      IFileSystemNameFactory fileNameFactory,
      IProject project,
      IProgressTracker progress,
      DirectoryName directory,
      bool isSymLink) {

      var ssw = new MultiStepStopWatch();
      // Create list of pairs (DirectoryName, List[FileNames])
      var directoriesWithFiles = TraverseFileSystem(_fileSystem, fileNameFactory, project, directory, isSymLink)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(traversedDirectoryEntry => {
          var directoryName = traversedDirectoryEntry.DirectoryData.DirectoryName;
          if (progress.Step()) {
            progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}", project.RootPath.Value, directoryName.RelativePath.Value));
          }
          var fileNames = traversedDirectoryEntry.ChildFileNames
            .Where(childFilename => project.FileFilter.Include(childFilename.RelativePath))
            .OrderBy(x => x.RelativePath)
            .ToReadOnlyCollection();

          return KeyValuePair.Create(traversedDirectoryEntry.DirectoryData, fileNames);
        })
        .ToList();

      // We sort entries by directory name *descending* to make sure we process
      // directories bottom up, so that we know
      // 1) it is safe to skip DirectoryEntry instances where "Entries.Count" == 0,
      // 2) we create instances of child directories before their parent.
      directoriesWithFiles.Sort((x, y) => -x.Key.DirectoryName.RelativePath.CompareTo(y.Key.DirectoryName.RelativePath));

      // Build map from parent directory -> list of child directories
      var directoriesToChildDirectories = new Dictionary<DirectoryName, List<DirectoryName>>();
      directoriesWithFiles.ForAll(x => {
        var directoryName = x.Key;

        // Ignore root project directory name
        if (directoryName.DirectoryName.IsAbsoluteName)
          return;

        GetOrCreateList(directoriesToChildDirectories, directoryName.DirectoryName.Parent).Add(directoryName.DirectoryName);
      });

      // Build directory snapshots for each directory entry, using an
      // intermediate map to enable connecting snapshots to their parent.
      var directoriesToSnapshot = new Dictionary<DirectoryName, DirectorySnapshot>();
      var directorySnapshots = directoriesWithFiles.Select(entry => {
        var directoryElement = entry.Key;
        var childFilenames = entry.Value;

        var childDirectories = GetOrEmptyList(directoriesToChildDirectories, directoryElement.DirectoryName)
          .Select(x => directoriesToSnapshot[x])
          .OrderBy(x => x.DirectoryName.RelativePath)
          .ToReadOnlyCollection();

        // TODO(rpaquay): Not clear the lines below are a perf win, even though
        // they do not hurt correctness.
        // Remove children since we processed them
        //GetOrEmptyList(directoriesToChildDirectories, directoryName)
        //  .ForAll(x => directoriesToSnapshot.Remove(x));

        var result = new DirectorySnapshot(directoryElement, childDirectories, childFilenames);
        directoriesToSnapshot.Add(directoryElement.DirectoryName, result);
        return result;
      })
        .ToList();

      // Since we sort directories by name descending, the last entry is always the
      // entry correcsponding to the project root.
      Debug.Assert(directorySnapshots.Count >= 1);
      Debug.Assert(directorySnapshots.Last().DirectoryName.Equals(directory));
      return directorySnapshots.Last();
    }

    private DirectorySnapshot ApplyDirectorySnapshotDelta(
      IFileSystemNameFactory fileSystemNameFactory,
      IProject project,
      IProgressTracker progress,
      DirectorySnapshot oldDirectory,
      FullPathChanges pathChanges) {

      var oldDirectoryPath = oldDirectory.DirectoryName.FullPath;

      // Create lists of created dirs and files.
      List<IFileInfoSnapshot> createDirs = null;
      List<IFileInfoSnapshot> createdFiles = null;
      foreach (var path in pathChanges.GetCreatedEntries(oldDirectoryPath).ToForeachEnum()) {
        var info = _fileSystem.GetFileInfoSnapshot(path);
        if (info.IsDirectory) {
          if (createDirs == null)
            createDirs = new List<IFileInfoSnapshot>();
          createDirs.Add(info);
        } else if (info.IsFile) {
          if (createdFiles == null)
            createdFiles = new List<IFileInfoSnapshot>();
          createdFiles.Add(info);
        }
      }

      // Match non deleted directories
      var childDirectories = oldDirectory.ChildDirectories
        .Where(x => !pathChanges.IsDeleted(x.DirectoryName.FullPath))
        .Select(x => ApplyDirectorySnapshotDelta(fileSystemNameFactory, project, progress, x, pathChanges))
        .ToList();

      // Add created directories
      if (createDirs != null) {
        foreach (var info in createDirs.ToForeachEnum()) {
          var name = fileSystemNameFactory.CreateDirectoryName(oldDirectory.DirectoryName, info.Path.FileName);
          var childSnapshot = CreateDirectorySnapshot(fileSystemNameFactory, project, progress, name, info.IsSymLink);
          childDirectories.Add(childSnapshot);
        }

        // We need to re-sort the array since we added new entries
        childDirectories.Sort((x, y) => 
          StringComparer.Ordinal.Compare(x.DirectoryName.RelativePath.FileName, y.DirectoryName.RelativePath.FileName));
      }

      // Match non deleted files
      // Sepcial case:
      IList<FileName> childFiles;
      if (pathChanges.GetDeletedEntries(oldDirectoryPath).Count == 0 && createdFiles == null) {
        childFiles = oldDirectory.ChildFiles;
      } else {
        childFiles = oldDirectory.ChildFiles
          .Where(x => !pathChanges.IsDeleted(x.FullPath))
          .ToList();

        // Add created files
        if (createdFiles != null) {
          foreach (var info in createdFiles) {
            var name = fileSystemNameFactory.CreateFileName(oldDirectory.DirectoryName, info.Path.FileName);
            childFiles.Add(name);
          }

          // We need to re-sort the array since we added new entries
          childFiles = childFiles.OrderBy(x => x.RelativePath.FileName, StringComparer.Ordinal).ToList();
        }
      }

      var data = new DirectoryData(oldDirectory.DirectoryName, oldDirectory.IsSymLink);

      return new DirectorySnapshot(
        data,
        childDirectories.ToReadOnlyCollection(),
        childFiles.ToReadOnlyCollection());
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

    /// <summary>
    /// Enumerate directories and files under the project path of |projet|.
    /// </summary>
    private static IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(
      IFileSystem fileSystem,
      IFileSystemNameFactory fileNameFactory,
      IProject project,
      DirectoryName startDirectoryName,
      bool isSymLink) {

      var stack = new Stack<DirectoryData>();
      stack.Push(new DirectoryData(startDirectoryName, isSymLink));
      while (stack.Count > 0) {
        var head = stack.Pop();
        if (head.DirectoryName.IsAbsoluteName || project.DirectoryFilter.Include(head.DirectoryName.RelativePath)) {
          var childEntries = fileSystem.GetDirectoryEntries(project.RootPath.Combine(head.DirectoryName.RelativePath));
          var childFileNames = new List<FileName>();
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childEntries.Count; i++) {
            DirectoryEntry entry = childEntries[i];
            if (entry.IsDirectory) {
              stack.Push(new DirectoryData(fileNameFactory.CreateDirectoryName(head.DirectoryName, entry.Name), entry.IsSymLink));
            } else if (entry.IsFile) {
              childFileNames.Add(fileNameFactory.CreateFileName(head.DirectoryName, entry.Name));
            }
          }
          yield return new TraversedDirectoryEntry(head, childFileNames);
        }
      }
    }

    private struct TraversedDirectoryEntry {
      private readonly DirectoryData _directoryData;
      private readonly IList<FileName> _childFileNames;

      public TraversedDirectoryEntry(DirectoryData directoryData, IList<FileName> childFileNames) {
        _directoryData = directoryData;
        _childFileNames = childFileNames;
      }

      public DirectoryData DirectoryData { get { return _directoryData; } }
      public IEnumerable<FileName> ChildFileNames { get { return _childFileNames; } }
    }
  }
}
