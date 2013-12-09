// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromiumCore;
using VsChromiumCore.FileNames;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumServer.FileSystemNames;
using VsChromiumServer.ProgressTracking;
using VsChromiumServer.Projects;
using VsChromiumServer.Threads;

namespace VsChromiumServer.FileSystem {
  [Export(typeof(IFileSystemProcessor))]
  public class FileSystemProcessor : IFileSystemProcessor {
    private readonly HashSet<string> _addedFiles = new HashSet<string>(SystemPathComparer.Instance.Comparer);
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IDirectoryChangeWatcher _directoryChangeWatcher;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly object _lock = new object();
    private readonly IOperationIdFactory _operationIdFactory;
    private readonly IProgressTrackerFactory _progressTrackerFactory;
    private readonly ITaskQueue _taskQueue;
    private DirectoryEntry _rootEntry = new DirectoryEntry();
    private int _version;

    [ImportingConstructor]
    public FileSystemProcessor(
        IFileSystemNameFactory fileSystemNameFactory,
        IProjectDiscovery projectDiscovery,
        IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
        IOperationIdFactory operationIdFactory,
        ITaskQueueFactory taskQueueFactory,
        IProgressTrackerFactory progressTrackerFactory) {
      this._fileSystemNameFactory = fileSystemNameFactory;
      this._directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher();
      this._operationIdFactory = operationIdFactory;
      this._progressTrackerFactory = progressTrackerFactory;
      this._projectDiscovery = projectDiscovery;
      this._taskQueue = taskQueueFactory.CreateQueue();
      this._directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
    }

    public FileSystemTree GetTree() {
      lock (this._lock) {
        return GetTree(this._version, this._rootEntry);
      }
    }

    public void AddFile(string filename) {
      this._taskQueue.Enqueue(string.Format("AddFile(\"{0}\")", filename), () => AddFileTask(filename));
    }

    public void RemoveFile(string filename) {
      this._taskQueue.Enqueue(string.Format("RemoveFile(\"{0}\")", filename), () => RemoveFileTask(filename));
    }

    public event Action<long> TreeComputing;
    public event Action<long, FileSystemTree, FileSystemTree> TreeComputed;
    public event Action<IEnumerable<FileName>> FilesChanged;

    private void DirectoryChangeWatcherOnPathsChanged(IList<KeyValuePair<string, ChangeType>> changes) {
      this._taskQueue.Enqueue("OnPathsChangedTask()", () => OnPathsChangedTask(changes));
    }

    private void OnPathsChangedTask(IList<KeyValuePair<string, ChangeType>> changes) {
      var result =
          new FileSystemTreeValidator(this._fileSystemNameFactory, this._projectDiscovery).ProcessPathsChangedEvent(changes);
      if (result.RecomputeGraph) {
        RecomputeGraph();
      } else if (result.ChangeFiles.Any()) {
        OnFilesChanged(result.ChangeFiles);
      }
    }

    private void AddFileTask(string filename) {
      bool recompute;

      lock (this._lock) {
        recompute = ValidateKnownFiles();

        if (this._addedFiles.Add(filename)) {
          var projectPath = this._projectDiscovery.GetProjectPath(filename);
          if (projectPath != null) {
            var known = this._rootEntry.Entries.Any(x => SystemPathComparer.Instance.Comparer.Equals(x.Name, projectPath));
            if (!known) {
              recompute = true;
            }
          }
        }
      }

      if (recompute)
        RecomputeGraph();
    }

    private void RemoveFileTask(string filename) {
      bool recompute;

      lock (this._lock) {
        recompute = ValidateKnownFiles();

        var known = this._addedFiles.Contains(filename);
        if (known) {
          this._addedFiles.Remove(filename);
          recompute = true;
        }
      }

      if (recompute)
        RecomputeGraph();
    }

    /// <summary>
    /// Sanety check: remove all files that don't exist on the file system anymore.
    /// </summary>
    private bool ValidateKnownFiles() {
      lock (this._lock) {
        var removedCount = this._addedFiles.RemoveWhere(x => !File.Exists(x));
        if (removedCount > 0) {
          Logger.Log("Some known files do not exist on disk anymore. Time to recompute the world.");
          this._projectDiscovery.ValidateCache();
          return true;
        }
        return false;
      }
    }

    private FileSystemTree GetTree(int version, DirectoryEntry root) {
      return new FileSystemTree {
        Version = version,
        Root = root
      };
    }

    private void RecomputeGraph() {
      var operationId = this._operationIdFactory.GetNextId();
      OnTreeComputing(operationId);
      Logger.Log("Collecting list of files from file system.");
      Logger.LogMemoryStats();
      var sw = Stopwatch.StartNew();

      var files = new List<string>();
      lock (this._lock) {
        ValidateKnownFiles();
        files.AddRange(this._addedFiles);
      }

      var newRoot =
          new FileSystemTreeBuilder(this._projectDiscovery, this._progressTrackerFactory).ComputeNewRoot(files);

      // Monitor all the Chromium directories for changes.
      var newRoots = newRoot.Entries
          .Select(root => this._fileSystemNameFactory.CombineDirectoryNames(this._fileSystemNameFactory.Root, root.Name))
          .ToList();
      this._directoryChangeWatcher.WatchDirectories(newRoots);

      // Update current tree atomically
      var oldTree = GetTree();
      lock (this._lock) {
        this._version++;
        this._rootEntry = newRoot;
      }
      var newTree = GetTree();

      sw.Stop();
      Logger.Log("Done collecting list of files: {0:n0} files in {1:n0} directories collected in {2:n0} msec.",
          CountFileEntries(newRoot), CountDirectoryEntries(newRoot), sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      // A new tree is available, time to notify our consumers.
      OnTreeComputed(operationId, oldTree, newTree);
    }

    private int CountFileEntries(FileSystemEntry entry) {
      if (entry == null)
        return 0;

      var dir = entry as DirectoryEntry;
      if (dir != null) {
        return dir.Entries.Aggregate(0, (s, x) => s + CountFileEntries(x));
      }

      return 1;
    }

    private int CountDirectoryEntries(FileSystemEntry entry) {
      if (entry == null)
        return 0;

      var dir = entry as DirectoryEntry;
      if (dir != null) {
        return dir.Entries.Aggregate(1, (s, x) => s + CountDirectoryEntries(x));
      }

      return 0;
    }

    protected virtual void OnTreeComputing(long operationId) {
      var handler = TreeComputing;
      if (handler != null)
        handler(operationId);
    }

    private void OnTreeComputed(long operationId, FileSystemTree oldTree, FileSystemTree newTree) {
      var handler = TreeComputed;
      if (handler != null)
        handler(operationId, oldTree, newTree);
    }

    private void OnFilesChanged(IEnumerable<FileName> paths) {
      var handler = FilesChanged;
      if (handler != null)
        handler(paths);
    }
  }
}
