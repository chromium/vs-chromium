// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IFileSystemProcessor))]
  public class FileSystemProcessor : IFileSystemProcessor {
    /// <summary>
    /// Performance optimization flag: when building a new file system tree
    /// snapshot, this flag enables code to try to re-use filename instances
    /// from the previous snapshot. This is currently turned off, as profiling
    /// showed this slows down the algorithm by about 40% with not advantage
    /// other than decreasing GC activity. Note that this actually didn't
    /// decrease memory usage, as FileName instances are orphaned when a new
    /// snapshot is created (and the previous one is released).
    /// </summary>
    private static readonly bool ReuseFileNameInstances = false;

    private readonly HashSet<FullPath> _addedFiles = new HashSet<FullPath>();
    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IDirectoryChangeWatcher _directoryChangeWatcher;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileSystem _fileSystem;
    private readonly object _lock = new object();
    private readonly IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    private readonly IOperationProcessor _operationProcessor;
    private readonly ITaskQueue _taskQueue;
    private FileSystemTreeSnapshot _fileSystemSnapshot;
    private int _version;

    [ImportingConstructor]
    public FileSystemProcessor(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      ITaskQueueFactory taskQueueFactory,
      IFileSystemSnapshotBuilder fileSystemSnapshotBuilder,
      IOperationProcessor operationProcessor) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher();
      _fileSystemSnapshotBuilder = fileSystemSnapshotBuilder;
      _operationProcessor = operationProcessor;
      _projectDiscovery = projectDiscovery;
      _taskQueue = taskQueueFactory.CreateQueue("FileSystemProcessor Task Queue");
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

    public event EventHandler<OperationInfo> SnapshotComputing;
    public event EventHandler<SnapshotComputedResult> SnapshotComputed;
    public event EventHandler<FilesChangedEventArgs> FilesChanged;

    protected virtual void OnSnapshotComputing(OperationInfo e) {
      EventHandler<OperationInfo> handler = SnapshotComputing;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnSnapshotComputed(SnapshotComputedResult e) {
      EventHandler<SnapshotComputedResult> handler = SnapshotComputed;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnFilesChanged(FilesChangedEventArgs e) {
      EventHandler<FilesChangedEventArgs> handler = FilesChanged;
      if (handler != null) handler(this, e);
    }

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      _taskQueue.Enqueue("OnPathsChangedTask()", () => OnPathsChangedTask(changes));
    }

    private void OnPathsChangedTask(IList<PathChangeEntry> changes) {
      var result =
        new FileSystemChangesValidator(_fileSystemNameFactory, _projectDiscovery).ProcessPathsChangedEvent(changes);
      if (result.RecomputeGraph) {
        RecomputeGraph();
      } else if (result.ChangedFiles.Any()) {
        OnFilesChanged(new FilesChangedEventArgs {
          ChangedFiles = result.ChangedFiles.ToReadOnlyCollection()
        });
      }
    }

    private void AddFileTask(string filename) {
      var path = new FullPath(filename);
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
      var path = new FullPath(filename);
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

    private IEnumerable<FullPath> GetKnownProjectPaths(IEnumerable<FullPath> knownFileNames) {
      return knownFileNames
        .Select(x => _projectDiscovery.GetProjectPath(x))
        .Where(x => x != default(FullPath))
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
      IList<FullPath> filenames;
      lock (_lock) {
        filenames = _addedFiles.ToList();
      }

      var deletedFileNames = filenames.Where(x => !_fileSystem.FileExists(x)).ToList();

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
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info => OnSnapshotComputing(info),
        OnError = (info, error) => OnSnapshotComputed(new SnapshotComputedResult { OperationInfo = info, Error = error }),
        Execute = info => {
          Logger.Log("Collecting list of files from file system.");
          Logger.LogMemoryStats();
          var sw = Stopwatch.StartNew();

          var files = new List<FullPath>();
          lock (_lock) {
            ValidateKnownFiles();
            files.AddRange(_addedFiles);
          }

          IFileSystemNameFactory fileNameFactory = _fileSystemNameFactory;
          if (ReuseFileNameInstances) {
            if (_fileSystemSnapshot.ProjectRoots.Count > 0) {
              fileNameFactory = new FileSystemTreeSnapshotNameFactory(_fileSystemSnapshot, fileNameFactory);
            }
          }
          var newSnapshot = _fileSystemSnapshotBuilder.Compute(fileNameFactory, files, Interlocked.Increment(ref _version));

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
          Logger.Log(">>>>>>>> Done collecting list of files: {0:n0} files in {1:n0} directories collected in {2:n0} msec.",
            newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountFileEntries(x.Directory)),
            newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x.Directory)),
            sw.ElapsedMilliseconds);
          Logger.LogMemoryStats();

          OnSnapshotComputed(new SnapshotComputedResult {
            OperationInfo = info,
            PreviousSnapshot = previousSnapshot,
            NewSnapshot = newSnapshot
          });
        }
      });
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
  }
}
