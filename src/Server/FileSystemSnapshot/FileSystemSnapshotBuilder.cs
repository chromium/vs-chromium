// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Collections;
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
      IList<FullPath> rootFiles,
      int version) {
      using (var progress = _progressTrackerFactory.CreateIndeterminateTracker()) {
        var projectRoots =
          rootFiles
            .Select(filename => _projectDiscovery.GetProject(filename))
            .Where(project => project != null)
            .Distinct(new ProjectPathComparer())
            .Select(project => {
              var processingContext = new ProjectProcessingContext {
                FileSystemNameFactory = fileNameFactory,
                Project = project,
                Progress = progress,
                OldSnapshot = oldSnapshot,
                PathChanges = (pathChanges == null ? null : new ProjectPathChanges(project.RootPath, pathChanges.Entries)),
              };
              var rootSnapshot = ProcessProject(processingContext);
              return new ProjectRootSnapshot(project, rootSnapshot);
            })
            .OrderBy(projectRoot => projectRoot.Directory.DirectoryName)
            .ToReadOnlyCollection();

        return new FileSystemTreeSnapshot(version, projectRoots);
      }
    }

    private class ProjectProcessingContext {
      public IFileSystemNameFactory FileSystemNameFactory { get; set; }
      public FileSystemTreeSnapshot OldSnapshot { get; set; }
      public IProject Project { get; set; }
      public IProgressTracker Progress { get; set; }
      public ProjectPathChanges PathChanges { get; set; }
    }

    private DirectorySnapshot ProcessProject(ProjectProcessingContext context) {

      if (context.PathChanges != null) {
        // If we have a project with the same root in the old snapshot, use that
        // snapshot instead of traversing the file system.
        var oldRoot = context.OldSnapshot.ProjectRoots
          .FirstOrDefault(x => x.Project.RootPath.Equals(context.Project.RootPath));
        if (oldRoot != null) {
          return ApplyDirectorySnapshotDelta(context, oldRoot.Directory);
        }
      }

      var projectPath = context.FileSystemNameFactory.CreateAbsoluteDirectoryName(context.Project.RootPath);
      return CreateDirectorySnapshot(context, projectPath, false);
    }

    private DirectorySnapshot CreateDirectorySnapshot(
      ProjectProcessingContext context,
      DirectoryName directory,
      bool isSymLink) {

      // Create list of pairs (DirectoryName, List[FileNames])
      var directoriesWithFiles = TraverseFileSystem(context, directory, isSymLink)
        .AsParallel()
        .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
        .Select(traversedDirectoryEntry => {
          var directoryName = traversedDirectoryEntry.DirectoryData.DirectoryName;
          if (context.Progress.Step()) {
            context.Progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}", context.Project.RootPath.Value, directoryName.RelativePath.Value));
          }
          var fileNames = traversedDirectoryEntry.ChildFileNames
            .Where(childFilename => context.Project.FileFilter.Include(childFilename.RelativePath))
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
      ProjectProcessingContext context, 
      DirectorySnapshot oldDirectory) {

      var oldDirectoryPath = oldDirectory.DirectoryName.RelativePath;

      // Create lists of created dirs and files. We havet to access the file system to know
      // if each path is a file or a directory.
      List<IFileInfoSnapshot> createDirs = null;
      List<IFileInfoSnapshot> createdFiles = null;
      foreach (var path in context.PathChanges.GetCreatedEntries(oldDirectoryPath).ToForeachEnum()) {
        var info = _fileSystem.GetFileInfoSnapshot(context.Project.RootPath.Combine(path));
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

      // Recursively create new directory entires for previous (non deleted)
      // entries.
      var childDirectories = oldDirectory.ChildDirectories
        .Where(dir => !context.PathChanges.IsDeleted(dir.DirectoryName.RelativePath))
        .Select(dir => ApplyDirectorySnapshotDelta(context, dir))
        .ToList();

      // Add created directories
      if (createDirs != null) {
        foreach (var info in createDirs.ToForeachEnum()) {
          var name = context.FileSystemNameFactory.CreateDirectoryName(oldDirectory.DirectoryName, info.Path.FileName);
          var childSnapshot = CreateDirectorySnapshot(context, name, info.IsSymLink);

          // Note: File system change notifications are not always 100%
          // reliable. We may get a "create" event for directory we already know
          // about.
          var index = childDirectories.FindIndex(x =>
              SystemPathComparer.Instance.StringComparer.Equals(x.DirectoryName.RelativePath.FileName, name.RelativePath.FileName));
          if (index >= 0) {
            childDirectories.RemoveAt(index);
          }
          childDirectories.Add(childSnapshot);
        }

        // We need to re-sort the array since we added new entries
        childDirectories.Sort((x, y) =>
          SystemPathComparer.Instance.StringComparer.Compare(x.DirectoryName.RelativePath.FileName, y.DirectoryName.RelativePath.FileName));
      }

      // Match non deleted files
      // Sepcial case: if no file deleted or created, just re-use the list.
      IList<FileName> newFileList;
      if (context.PathChanges.GetDeletedEntries(oldDirectoryPath).Count == 0 && createdFiles == null) {
        newFileList = oldDirectory.ChildFiles;
      } else {
        // Copy the list of previous children, minus deleted files.
        var newFileListTemp = oldDirectory.ChildFiles
          .Where(x => !context.PathChanges.IsDeleted(x.RelativePath))
          .ToList();

        // Add created files
        if (createdFiles != null) {
          foreach (var info in createdFiles.ToForeachEnum()) {
            var name = context.FileSystemNameFactory.CreateFileName(oldDirectory.DirectoryName, info.Path.FileName);
            newFileListTemp.Add(name);
          }

          // We need to re-sort the array since we added new entries
          newFileListTemp.Sort((x, y) =>
            SystemPathComparer.Instance.StringComparer.Compare(x.RelativePath.FileName, y.RelativePath.FileName));

          // Note: File system change notifications are not always 100%
          // reliable. We may get a "create" event for files we already know
          // about.
          ArrayUtilities.RemoveDuplicates(newFileListTemp, (x, y) =>
            SystemPathComparer.Instance.StringComparer.Equals(x.RelativePath.FileName, y.RelativePath.FileName));
        }
        newFileList = newFileListTemp;
      }

      var newData = new DirectoryData(oldDirectory.DirectoryName, oldDirectory.IsSymLink);

      return new DirectorySnapshot(
        newData,
        childDirectories.ToReadOnlyCollection(),
        newFileList.ToReadOnlyCollection());
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
    private IEnumerable<TraversedDirectoryEntry> TraverseFileSystem(
      ProjectProcessingContext context,
      DirectoryName startDirectoryName,
      bool isSymLink) {

      var stack = new Stack<DirectoryData>();
      stack.Push(new DirectoryData(startDirectoryName, isSymLink));
      while (stack.Count > 0) {
        var head = stack.Pop();
        if (head.DirectoryName.IsAbsoluteName || context.Project.DirectoryFilter.Include(head.DirectoryName.RelativePath)) {
          var childEntries = _fileSystem.GetDirectoryEntries(context.Project.RootPath.Combine(head.DirectoryName.RelativePath));
          var childFileNames = new List<FileName>();
          // Note: Use "for" loop to avoid memory allocations.
          for (var i = 0; i < childEntries.Count; i++) {
            DirectoryEntry entry = childEntries[i];
            if (entry.IsDirectory) {
              stack.Push(new DirectoryData(context.FileSystemNameFactory.CreateDirectoryName(head.DirectoryName, entry.Name), entry.IsSymLink));
            } else if (entry.IsFile) {
              childFileNames.Add(context.FileSystemNameFactory.CreateFileName(head.DirectoryName, entry.Name));
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
