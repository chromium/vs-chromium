// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem.Builder {
  public class ProjectRootSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly FileSystemSnapshot _oldSnapshot;
    private readonly IProject _project;
    private readonly IProgressTracker _progress;
    private readonly ProjectPathChanges _pathChanges;
    private readonly CancellationToken _cancellationToken;

    public ProjectRootSnapshotBuilder(IFileSystem fileSystem,
                                     IFileSystemNameFactory fileSystemNameFactory,
                                     FileSystemSnapshot oldSnapshot,
                                     IProject project,
                                     IProgressTracker progress,
                                     ProjectPathChanges pathChanges,
                                     CancellationToken cancellationToken) {
      _fileSystem = fileSystem;
      _fileSystemNameFactory = fileSystemNameFactory;
      _oldSnapshot = oldSnapshot;
      _project = project;
      _progress = progress;
      _pathChanges = pathChanges;
      _cancellationToken = cancellationToken;
    }

    public DirectorySnapshot Build() {
      if (_pathChanges != null) {
        // If we have a project with the same root in the old snapshot, use that
        // snapshot instead of traversing the file system.
        var oldRoot = _oldSnapshot.ProjectRoots
          .FirstOrDefault(x => x.Project.RootPath.Equals(_project.RootPath));
        if (oldRoot != null) {
          return ApplyDirectorySnapshotDelta(oldRoot.Directory);
        }
      }

      return CreateProjectDirectorySnapshot();
    }

    private DirectorySnapshot CreateProjectDirectorySnapshot() {
      using (new TimeElapsedLogger($"Creating directory snapshot from file system files of \"{_project.RootPath}\"", _cancellationToken)) {
        var projectPath = _fileSystemNameFactory.CreateAbsoluteDirectoryName(_project.RootPath);
        return CreateDirectorySnapshot(projectPath, false);
      }
    }

    private DirectorySnapshot CreateDirectorySnapshot(DirectoryName directory, bool isSymLink) {
      _cancellationToken.ThrowIfCancellationRequested();

      var directoriesWithFiles = TraverseFileSystem(directory, isSymLink).ToList();

      // We sort entries by directory name *descending* to make sure we process
      // directories bottom up, so that we know
      // 1) it is safe to skip DirectoryEntry instances where "Entries.Count" == 0,
      // 2) we create instances of child directories before their parent.
      _progress.DisplayProgress((i, n) => $"Sorting files of directory {directory.FullPath}");
      directoriesWithFiles.Sort((x, y) => -x.DirectoryData.DirectoryName.CompareTo(y.DirectoryData.DirectoryName));

      // Build map from parent directory -> list of child directories
      var directoriesToChildDirectories = new Dictionary<DirectoryName, List<DirectoryName>>();
      directoriesWithFiles.ForAll(x => {
        _cancellationToken.ThrowIfCancellationRequested();
        var directoryName = x.DirectoryData.DirectoryName;

        // Ignore root project directory name
        if (directoryName.IsAbsoluteName)
          return;

        GetOrCreateList(directoriesToChildDirectories, directoryName.Parent)
          .Add(directoryName);
      });

      // Build directory snapshots for each directory entry, using an
      // intermediate map to enable connecting snapshots to their parent.
      var directoriesToSnapshot = new Dictionary<DirectoryName, DirectorySnapshot>();
      var directorySnapshots = directoriesWithFiles.Select(entry => {
        _cancellationToken.ThrowIfCancellationRequested();
        if (_progress.Step()) {
          _progress.DisplayProgress((i, n) => $"Processing files and directories of directory {_project.RootPath.Value}");
        }

        var directoryData = entry.DirectoryData;
        var childFileNames = entry.FileNames;

        var childDirectories = GetOrEmptyList(directoriesToChildDirectories, directoryData.DirectoryName)
          .Select(x => directoriesToSnapshot[x])
          .OrderBy(x => x.DirectoryName.Name)
          .ToReadOnlyList();

        // TODO(rpaquay): Not clear the lines below are a perf win, even though
        // they do not hurt correctness.
        // Remove children since we processed them
        //GetOrEmptyList(directoriesToChildDirectories, directoryName)
        //  .ForAll(x => directoriesToSnapshot.Remove(x));

        var result = new DirectorySnapshot(directoryData, childDirectories, childFileNames);
        directoriesToSnapshot.Add(directoryData.DirectoryName, result);
        return result;
      })
        .ToList();

      // Since we sort directories by name descending, the last entry is always the
      // entry correcsponding to the project root.
      Invariants.Assert(directorySnapshots.Count >= 1);
      Invariants.Assert(directorySnapshots.Last().DirectoryName.Equals(directory));
      return directorySnapshots.Last();
    }

    private DirectorySnapshot ApplyDirectorySnapshotDelta(DirectorySnapshot oldDirectory) {
      var oldDirectoryPath = oldDirectory.DirectoryName.RelativePath;

      // Create lists of created dirs and files. We have to access the file system to know
      // if each path is a file or a directory.
      List<IFileInfoSnapshot> createDirs = null;
      List<IFileInfoSnapshot> createdFiles = null;
      foreach (var path in _pathChanges.GetCreatedEntries(oldDirectoryPath).ToForeachEnum()) {
        _cancellationToken.ThrowIfCancellationRequested(); // cancellation

        var info = _fileSystem.GetFileInfoSnapshot(_project.RootPath.Combine(path));
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
        .Where(dir => !_pathChanges.IsDeleted(dir.DirectoryName.RelativePath))
        .Select(dir => ApplyDirectorySnapshotDelta(dir))
        .ToList();

      // Add created directories
      if (createDirs != null) {
        foreach (var info in createDirs.ToForeachEnum()) {
          _cancellationToken.ThrowIfCancellationRequested(); // cancellation

          var createdDirectoryName = _fileSystemNameFactory.CreateDirectoryName(oldDirectory.DirectoryName, info.Path.FileName);
          var childSnapshot = CreateDirectorySnapshot(createdDirectoryName, info.IsSymLink);

          // Note: File system change notifications are not always 100%
          // reliable. We may get a "create" event for directory we already know
          // about.
          var index = childDirectories.FindIndex(x => SystemPathComparer.EqualsNames(x.DirectoryName.Name, createdDirectoryName.Name));
          if (index >= 0) {
            childDirectories.RemoveAt(index);
          }
          childDirectories.Add(childSnapshot);
        }

        // We need to re-sort the array since we added new entries
        childDirectories.Sort((x, y) => SystemPathComparer.Compare(x.DirectoryName.Name, y.DirectoryName.Name));
      }

      // Match non deleted files
      // Sepcial case: if no file deleted or created, just re-use the list.
      IList<FileName> newFileList;
      if (_pathChanges.GetDeletedEntries(oldDirectoryPath).Count == 0 && createdFiles == null) {
        newFileList = oldDirectory.ChildFiles;
      } else {
        // Copy the list of previous children, minus deleted files.
        var newFileListTemp = oldDirectory.ChildFiles
          .Where(x => !_pathChanges.IsDeleted(x.RelativePath))
          .ToList();

        // Add created files
        if (createdFiles != null) {
          foreach (var info in createdFiles.ToForeachEnum()) {
            var name = _fileSystemNameFactory.CreateFileName(oldDirectory.DirectoryName, info.Path.FileName);
            newFileListTemp.Add(name);
          }

          // We need to re-sort the array since we added new entries
          newFileListTemp.Sort((x, y) => SystemPathComparer.Compare(x.Name, y.Name));

          // Note: File system change notifications are not always 100%
          // reliable. We may get a "create" event for files we already know
          // about.
          ArrayUtilities.RemoveDuplicates(newFileListTemp, (x, y) => SystemPathComparer.EqualsNames(x.Name, y.Name));
        }
        newFileList = newFileListTemp;
      }

      var newData = new DirectoryData(oldDirectory.DirectoryName, oldDirectory.IsSymLink);

      return new DirectorySnapshot(
        newData,
        childDirectories.ToReadOnlyList(),
        newFileList.ToReadOnlyList());
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
    private IEnumerable<DirectoryWithFiles> TraverseFileSystem(DirectoryName directoryName, bool isSymLink) {
      using (new TimeElapsedLogger($"Traversing directory \"{directoryName.FullPath}\" to collect directory/file names", _cancellationToken)) {
        var directory = new DirectoryData(directoryName, isSymLink);
        var bag = new ConcurrentBag<DirectoryWithFiles>();
        var task = TraverseDirectoryAsync(directory, bag, _cancellationToken);
        task.Wait(_cancellationToken);
        return bag;
      }
    }

    private Task TraverseDirectoryAsync(DirectoryData directory, ConcurrentBag<DirectoryWithFiles> bag, CancellationToken token) {
      var directoryTask = Task.Run(() => ProcessDirectory(directory, bag, token), token);
      return directoryTask.ContinueWithTask(t => Task.WhenAll(t.Result), token);
    }

    private List<Task> ProcessDirectory(DirectoryData directory, ConcurrentBag<DirectoryWithFiles> bag, CancellationToken token) {
      var allTasks = new List<Task>();
      if (directory.DirectoryName.IsAbsoluteName ||
          _project.DirectoryFilter.Include(directory.DirectoryName.RelativePath)) {
        if (_progress.Step()) {
          _progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}",
            _project.RootPath.Value,
            directory.DirectoryName.RelativePath.Value));
        }

        var childEntries =
          _fileSystem.GetDirectoryEntries(_project.RootPath.Combine(directory.DirectoryName.RelativePath));
        var childFileNames = new List<FileName>();
        // Note: Use "for" loop to avoid memory allocations.
        for (var i = 0; i < childEntries.Count; i++) {
          var entry = childEntries[i];
          if (entry.IsDirectory) {
            var subDirName = _fileSystemNameFactory.CreateDirectoryName(directory.DirectoryName, entry.Name);
            var subDir = new DirectoryData(subDirName, entry.IsSymLink);
            var subDirTask = TraverseDirectoryAsync(subDir, bag, token);
            allTasks.Add(subDirTask);
          } else if (entry.IsFile) {
            childFileNames.Add(_fileSystemNameFactory.CreateFileName(directory.DirectoryName, entry.Name));
          }
        }

        var fileNames = childFileNames
          .Where(childFilename => _project.FileFilter.Include(childFilename.RelativePath))
          .OrderBy(x => x.Name)
          .ToReadOnlyList();

        bag.Add(new DirectoryWithFiles(directory, fileNames));
      }

      return allTasks;
    }

    private struct DirectoryWithFiles {
      private readonly DirectoryData _directory;
      private readonly IList<FileName> _fileNames;

      public DirectoryWithFiles(DirectoryData directory, IList<FileName> fileNames) {
        _directory = directory;
        _fileNames = fileNames;
      }

      public DirectoryData DirectoryData => _directory;
      public IList<FileName> FileNames => _fileNames;
    }
  }
}
