// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IFileSystemProcessor))]
  public class FileSystemProcessor : IFileSystemProcessor {
    private readonly HashSet<FullPathName> _addedFiles = new HashSet<FullPathName>();
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IDirectoryChangeWatcher _directoryChangeWatcher;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly object _lock = new object();
    private readonly IOperationIdFactory _operationIdFactory;
    private readonly IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    private readonly ITaskQueue _taskQueue;
    private FileSystemTreeSnapshot _fileSystemSnapshot;
    private int _version;

    [ImportingConstructor]
    public FileSystemProcessor(
      IFileSystemNameFactory fileSystemNameFactory,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      IOperationIdFactory operationIdFactory,
      ITaskQueueFactory taskQueueFactory,
      IFileSystemSnapshotBuilder fileSystemSnapshotBuilder) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher();
      _operationIdFactory = operationIdFactory;
      _fileSystemSnapshotBuilder = fileSystemSnapshotBuilder;
      _projectDiscovery = projectDiscovery;
      _taskQueue = taskQueueFactory.CreateQueue();
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _fileSystemSnapshot = FileSystemTreeSnapshot.Empty;
    }

    public FileSystemTreeSnapshot GetCurrentSnapshot() {
      lock (_lock) {
        return _fileSystemSnapshot;
      }
    }

    public void AddFile(string filename) {
      _taskQueue.Enqueue(string.Format("AddFile(\"{0}\")", filename), () => AddFileTask(filename));
    }

    public void RemoveFile(string filename) {
      _taskQueue.Enqueue(string.Format("RemoveFile(\"{0}\")", filename), () => RemoveFileTask(filename));
    }

    public event SnapshotComputingDelegate SnapshotComputing;
    public event SnapshotComputedDelegate SnapshotComputed;
    public event FilesChangedDelegate FilesChanged;

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      _taskQueue.Enqueue("OnPathsChangedTask()", () => OnPathsChangedTask(changes));
    }

    private void OnPathsChangedTask(IList<PathChangeEntry> changes) {
      var result =
        new FileSystemChangesValidator(_fileSystemNameFactory, _projectDiscovery).ProcessPathsChangedEvent(changes);
      if (result.RecomputeGraph) {
        RecomputeGraph();
      } else if (result.ChangedFiles.Any()) {
        OnFilesChanged(result.ChangedFiles);
      }
    }

    private void AddFileTask(string filename) {
      var path = new FullPathName(filename);
      bool recompute = ValidateKnownFiles();

      lock (_lock) {
        var known = _addedFiles.Contains(path);
        if (!known) {
          var projectPaths1 = GetKnownProjectPaths(_addedFiles);
          _addedFiles.Add(path);
          var projectPaths2 = GetKnownProjectPaths(_addedFiles);
          if (!projectPaths1.SequenceEqual(projectPaths2)) {
            recompute = true;
          }
        }
      }

      if (recompute)
        RecomputeGraph();
    }

    private void RemoveFileTask(string filename) {
      var path = new FullPathName(filename);
      bool recompute = ValidateKnownFiles();

      lock (_lock) {
        var known = _addedFiles.Contains(path);
        if (known) {
          var projectPaths1 = GetKnownProjectPaths(_addedFiles);
          _addedFiles.Remove(path);
          var projectPaths2 = GetKnownProjectPaths(_addedFiles);
          if (!projectPaths1.SequenceEqual(projectPaths2)) {
            recompute = true;
          }
        }
      }

      if (recompute)
        RecomputeGraph();
    }

    private IEnumerable<FullPathName> GetKnownProjectPaths(IEnumerable<FullPathName> knownFileNames) {
      return knownFileNames
        .Select(x => _projectDiscovery.GetProjectPath(x))
        .Where(x => x != default(FullPathName))
        .Distinct()
        .OrderBy(x => x)
        .ToList();
    }

    /// <summary>
    /// Sanety check: remove all files that don't exist on the file system anymore.
    /// </summary>
    private bool ValidateKnownFiles() {
      // We take the lock twice because we want to avoid calling "File.Exists" inside
      // the lock.
      IList<FullPathName> filenames;
      lock (_lock) {
        filenames = _addedFiles.ToList();
      }

      var deletedFileNames = filenames.Where(x => !File.Exists(x.FullName)).ToList();

      if (deletedFileNames.Any()) {
        Logger.Log("Some known files do not exist on disk anymore. Time to recompute the world.");
        lock (_lock) {
          deletedFileNames.ForEach(x => _addedFiles.Remove(x));
        }
        _projectDiscovery.ValidateCache();
        return true;
      }

      return false;
    }

    private void RecomputeGraph() {
      var operationId = _operationIdFactory.GetNextId();
      OnTreeComputing(operationId);
      Logger.Log("Collecting list of files from file system.");
      Logger.LogMemoryStats();
      var sw = Stopwatch.StartNew();

      var files = new List<FullPathName>();
      lock (_lock) {
        ValidateKnownFiles();
        files.AddRange(_addedFiles);
      }

      var newSnapshot = _fileSystemSnapshotBuilder.Compute(files, Interlocked.Increment(ref _version));

      // Monitor all the Chromium directories for changes.
      var newRoots = newSnapshot.ProjectRoots
        .Select(entry => entry.Directory.DirectoryName);
      _directoryChangeWatcher.WatchDirectories(newRoots);

      // Update current tree atomically
      FileSystemTreeSnapshot previousSnapshot;
      lock (_lock) {
        previousSnapshot = _fileSystemSnapshot;
        _fileSystemSnapshot = newSnapshot;
      }

      sw.Stop();
      Logger.Log("Done collecting list of files: {0:n0} files in {1:n0} directories collected in {2:n0} msec.",
                 newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountFileEntries(x.Directory)),
                 newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x.Directory)),
                 sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      // A new tree is available, time to notify our consumers.
      OnTreeComputed(operationId, previousSnapshot, newSnapshot);
    }

    private int CountFileEntries(DirectorySnapshot entry) {
      return
        entry.Files.Count +
        entry.DirectoryEntries.Aggregate(0, (acc, x) => acc + CountFileEntries(x));
    }

    private int CountDirectoryEntries(DirectorySnapshot entry) {
      return 
        1 +
        entry.DirectoryEntries.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x));
    }

    protected virtual void OnTreeComputing(long operationId) {
      var handler = SnapshotComputing;
      if (handler != null)
        handler(operationId);
    }

    private void OnTreeComputed(long operationId, FileSystemTreeSnapshot oldTree, FileSystemTreeSnapshot newTree) {
      var handler = SnapshotComputed;
      if (handler != null)
        handler(operationId, oldTree, newTree);
    }

    private void OnFilesChanged(IEnumerable<Tuple<IProject, FileName>> paths) {
      var handler = FilesChanged;
      if (handler != null)
        handler(paths);
    }
  }
}
