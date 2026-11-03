// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
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
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem.Builder {
  public class ProjectRootSnapshotBuilder {
    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly FileSystemSnapshot _oldSnapshot;
    private readonly IProject _project;
    private readonly IProgressTracker _progress;
    private readonly ProjectPathChanges _pathChanges;
    private readonly IIndexingWorkerPool _indexingWorkerPool;
    private readonly CancellationToken _cancellationToken;

    public ProjectRootSnapshotBuilder(IFileSystem fileSystem,
                                     IFileSystemNameFactory fileSystemNameFactory,
                                     FileSystemSnapshot oldSnapshot,
                                     IProject project,
                                     IProgressTracker progress,
                                     ProjectPathChanges pathChanges,
                                     IIndexingWorkerPool indexingWorkerPool,
                                     CancellationToken cancellationToken) {
      _fileSystem = fileSystem;
      _fileSystemNameFactory = fileSystemNameFactory;
      _oldSnapshot = oldSnapshot;
      _project = project;
      _progress = progress;
      _pathChanges = pathChanges;
      _indexingWorkerPool = indexingWorkerPool;
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

      using (new TimeElapsedLogger($"Traversing directory \"{directory.FullPath}\" to collect directory/file names", _cancellationToken, InfoLogger.Instance)) {
        // The snapshot tree is built bottom up during the traversal: each
        // directory task waits for its child directory tasks, then creates its
        // own snapshot (same design as mtsuite "IDirectorCollector<T>").
        var task = TraverseDirectoryAsync(new DirectoryData(directory, isSymLink), _cancellationToken);
        task.Wait(_cancellationToken);
        var result = task.Result;
        Invariants.Assert(result != null);
        Invariants.Assert(result.DirectoryName.Equals(directory));
        return result;
      }
    }

    private DirectorySnapshot ApplyDirectorySnapshotDelta(DirectorySnapshot oldDirectory) {
      var oldDirectoryPath = oldDirectory.DirectoryName.RelativePath;

      // Directories with no created or deleted entry in their subtree are unchanged (snapshots
      // are immutable): reuse them, instead of visiting (and building the relative path of) all
      // the directories of the project for each change.
      if (!_pathChanges.IsSubtreeAffected(oldDirectoryPath)) {
        return oldDirectory;
      }

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

          var createdDirectoryName = _fileSystemNameFactory.CreateDirectoryName(oldDirectory.DirectoryName, info.Path.FileName, info.IsSymLink);
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

    /// <summary>
    /// Returns a task that enumerates the files and directories of
    /// <paramref name="directory"/> (recursively) and returns the corresponding
    /// snapshot, or <code>null</code> if the directory is excluded by the
    /// project directory filter.
    /// </summary>
    private Task<DirectorySnapshot> TraverseDirectoryAsync(DirectoryData directory, CancellationToken token) {
      return Task.Factory.StartNew(
        () => ProcessDirectoryAsync(directory, token),
        token,
        TaskCreationOptions.DenyChildAttach,
        _indexingWorkerPool.Scheduler).Unwrap();
    }

    private async Task<DirectorySnapshot> ProcessDirectoryAsync(DirectoryData directory, CancellationToken token) {
      if (!directory.DirectoryName.IsAbsoluteName &&
          !directory.DirectoryName.TestRelativePath(static (path, filter) => filter.Include(path), _project.DirectoryFilter)) {
        return null;
      }

      List<Task<DirectorySnapshot>> childTasks = null;
      var childEntries = GetDirectoryEntries(directory);
      var childFileNames = new List<FileName>();
      // Note: Use "for" loop to avoid memory allocations.
      for (var i = 0; i < childEntries.Count; i++) {
        var entry = childEntries[i];
        if (entry.IsDirectory) {
          var subDirName = _fileSystemNameFactory.CreateDirectoryName(directory.DirectoryName, entry.Name, entry.IsSymLink);
          var subDir = new DirectoryData(subDirName, entry.IsSymLink);
          if (childTasks == null)
            childTasks = new List<Task<DirectorySnapshot>>();
          childTasks.Add(TraverseDirectoryAsync(subDir, token));
        } else if (entry.IsFile) {
          childFileNames.Add(_fileSystemNameFactory.CreateFileName(directory.DirectoryName, entry.Name));
        }
      }

      var fileNames = childFileNames
        .Where(childFilename => childFilename.TestRelativePath(static (path, filter) => filter.Include(path), _project.FileFilter))
        // Note: Ordinal (case-insensitive on Windows/macOS) sort, same as ApplyDirectorySnapshotDelta
        // (see "Behavior changes" in PORTING_PRINCIPLES.md).
        .OrderBy(x => x.Name, SystemPathComparer.NameComparer)
        .ToReadOnlyList();

      // Note: The progress of the traversal counts the files found (displayed by the clients).
      if (_progress.Step(fileNames.Count)) {
        _progress.DisplayProgress((i, n) => string.Format("Traversing directory: {0}\\{1}",
          _project.RootPath.Value,
          directory.DirectoryName.RelativePath.Value));
      }

      IList<DirectorySnapshot> childDirectories;
      if (childTasks == null) {
        childDirectories = ArrayUtilities.EmptyList<DirectorySnapshot>.Instance;
      } else {
        // Schedule the continuation on TaskScheduler.Current (the indexing worker pool) rather
        // than escaping back to the default thread pool.
        var childSnapshots = await Task.WhenAll(childTasks);
        childDirectories = childSnapshots
          .Where(x => x != null)
          .OrderBy(x => x.DirectoryName.Name, SystemPathComparer.NameComparer)
          .ToReadOnlyList();
      }

      return new DirectorySnapshot(directory, childDirectories, fileNames);
    }

    private IList<DirectoryEntry> GetDirectoryEntries(DirectoryData directory) {
      var path = _project.RootPath.Combine(directory.DirectoryName.RelativePath);
      try {
        return _fileSystem.GetDirectoryEntries(path);
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Skipping directory \"{0}\"", path);
        return ArrayUtilities.EmptyList<DirectoryEntry>.Instance;
      }
    }
  }
}
