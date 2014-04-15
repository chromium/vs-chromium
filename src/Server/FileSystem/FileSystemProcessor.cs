// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.IO;
using System.Linq;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemTree;
using VsChromium.Server.ProgressTracking;
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
    private readonly IFileSystemTreeBuilder _fileSystemTreeBuilder;
    private readonly ITaskQueue _taskQueue;
    private FileSystemTreeInternal _fileSystemTree;
    private int _version;

    [ImportingConstructor]
    public FileSystemProcessor(
      IFileSystemNameFactory fileSystemNameFactory,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      IOperationIdFactory operationIdFactory,
      ITaskQueueFactory taskQueueFactory,
      IFileSystemTreeBuilder fileSystemTreeBuilder) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher();
      _operationIdFactory = operationIdFactory;
      _fileSystemTreeBuilder = fileSystemTreeBuilder;
      _projectDiscovery = projectDiscovery;
      _taskQueue = taskQueueFactory.CreateQueue();
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _fileSystemTree = FileSystemTreeInternal.Empty(fileSystemNameFactory);
    }

    public VersionedFileSystemTreeInternal GetTree() {
      lock (_lock) {
        return GetTree(_version, _fileSystemTree);
      }
    }

    public void AddFile(string filename) {
      _taskQueue.Enqueue(string.Format("AddFile(\"{0}\")", filename), () => AddFileTask(filename));
    }

    public void RemoveFile(string filename) {
      _taskQueue.Enqueue(string.Format("RemoveFile(\"{0}\")", filename), () => RemoveFileTask(filename));
    }

    public event Action<long> TreeComputing;
    public event Action<long, VersionedFileSystemTreeInternal, VersionedFileSystemTreeInternal> TreeComputed;
    public event Action<IEnumerable<FileName>> FilesChanged;

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      _taskQueue.Enqueue("OnPathsChangedTask()", () => OnPathsChangedTask(changes));
    }

    private void OnPathsChangedTask(IList<PathChangeEntry> changes) {
      var result =
        new FileSystemTreeValidator(_fileSystemNameFactory, _projectDiscovery).ProcessPathsChangedEvent(changes);
      if (result.RecomputeGraph) {
        RecomputeGraph();
      } else if (result.ChangeFiles.Any()) {
        OnFilesChanged(result.ChangeFiles);
      }
    }

    private void AddFileTask(string filename2) {
      var filename = new FullPathName(filename2);
      bool recompute = ValidateKnownFiles();

      lock (_lock) {
        var known = _addedFiles.Contains(filename);
        if (!known) {
          var projectPaths1 = GetKnownProjectPaths(_addedFiles);
          _addedFiles.Add(filename);
          var projectPaths2 = GetKnownProjectPaths(_addedFiles);
          if (!projectPaths1.SequenceEqual(projectPaths2)) {
            recompute = true;
          }
        }
      }

      if (recompute)
        RecomputeGraph();
    }

    private void RemoveFileTask(string filename2) {
      var filename = new FullPathName(filename2);
      bool recompute = ValidateKnownFiles();

      lock (_lock) {
        var known = _addedFiles.Contains(filename);
        if (known) {
          var projectPaths1 = GetKnownProjectPaths(_addedFiles);
          _addedFiles.Remove(filename);
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
        .Where(x => x != null)
        .Select(x => new FullPathName(x))
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

    private VersionedFileSystemTreeInternal GetTree(int version, FileSystemTreeInternal fileSystemTree) {
      return new VersionedFileSystemTreeInternal(fileSystemTree, version);
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

      var newFileSystemTree = _fileSystemTreeBuilder.ComputeTree(files);

      // Monitor all the Chromium directories for changes.
      var newRoots = newFileSystemTree.Root.Entries
        .OfType<DirectoryEntryInternal>()
        .Select(entry => entry.Name)
        .ToList();
      _directoryChangeWatcher.WatchDirectories(newRoots);

      // Update current tree atomically
      var oldTree = GetTree();
      lock (_lock) {
        _version++;
        _fileSystemTree = newFileSystemTree;
      }
      var newTree = GetTree();

      sw.Stop();
      Logger.Log("Done collecting list of files: {0:n0} files in {1:n0} directories collected in {2:n0} msec.",
                 CountFileEntries(newFileSystemTree.Root), CountDirectoryEntries(newFileSystemTree.Root), sw.ElapsedMilliseconds);
      Logger.LogMemoryStats();

      // A new tree is available, time to notify our consumers.
      OnTreeComputed(operationId, oldTree, newTree);
    }

    private int CountFileEntries(FileSystemEntryInternal entry) {
      if (entry == null)
        return 0;

      var dir = entry as DirectoryEntryInternal;
      if (dir != null) {
        return dir.Entries.Aggregate(0, (s, x) => s + CountFileEntries(x));
      }

      return 1;
    }

    private int CountDirectoryEntries(FileSystemEntryInternal entry) {
      if (entry == null)
        return 0;

      var dir = entry as DirectoryEntryInternal;
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

    private void OnTreeComputed(long operationId, VersionedFileSystemTreeInternal oldTree, VersionedFileSystemTreeInternal newTree) {
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
