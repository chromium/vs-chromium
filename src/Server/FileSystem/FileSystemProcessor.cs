// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IFileSystemProcessor))]
  public class FileSystemProcessor : IFileSystemProcessor {
    private static readonly TaskId FlushPathsChangedQueueTaskId = new TaskId("FlushPathsChangedQueueTaskId");
    private static readonly TaskId ProjectListChangedTaskId = new TaskId("ProjectListChangedTaskId");
    private static readonly TaskId FullRescanRequiredTaskId = new TaskId("FullRescanRequiredTaskId");

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

    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IDirectoryChangeWatcher _directoryChangeWatcher;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    private readonly IOperationProcessor _operationProcessor;
    private readonly ITaskQueue _taskQueue;
    private readonly SimpleConcurrentQueue<IList<PathChangeEntry>> _pathsChangedQueue = new SimpleConcurrentQueue<IList<PathChangeEntry>>();
    private readonly CancellationTokenTracker _rescanCancellationTracker = new CancellationTokenTracker();
    private readonly FileRegistrationTracker _fileRegistrationTracker;
    /// <summary>
    /// Access to this field is serialized through tasks executed on the _taskQueue
    /// </summary>
    private IList<IProject> _registeredProjects = new List<IProject>();
    /// <summary>
    /// Access to this field is serialized through tasks executed on the _taskQueue
    /// </summary>
    private FileSystemTreeSnapshot _fileSystemSnapshot;
    private int _version;

    [ImportingConstructor]
    public FileSystemProcessor(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IFileSystemSnapshotBuilder fileSystemSnapshotBuilder,
      IOperationProcessor operationProcessor,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      ITaskQueueFactory taskQueueFactory) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _fileSystemSnapshotBuilder = fileSystemSnapshotBuilder;
      _operationProcessor = operationProcessor;
      _projectDiscovery = projectDiscovery;
      _fileRegistrationTracker = new FileRegistrationTracker(fileSystem, projectDiscovery, taskQueueFactory);

      _taskQueue = taskQueueFactory.CreateQueue("FileSystemProcessor Task Queue");
      _fileRegistrationTracker.ProjectListChanged += FileRegistrationTrackerOnProjectListChanged;
      _fileRegistrationTracker.FullRescanRequired += FileRegistrationTrackerOnFullRescanRequired;
      _fileSystemSnapshot = FileSystemTreeSnapshot.Empty;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher();
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _directoryChangeWatcher.Error += DirectoryChangeWatcherOnError;
    }

    public FileSystemTreeSnapshot CurrentSnapshot {
      get { return _fileSystemSnapshot; }
    }

    public void Refresh() {
      _fileRegistrationTracker.Refresh();
    }

    public void RegisterFile(FullPath path) {
      _fileRegistrationTracker.RegisterFile(path);
    }

    public void UnregisterFile(FullPath path) {
      _fileRegistrationTracker.UnregisterFile(path);
    }

    public event EventHandler<OperationInfo> SnapshotScanStarted;
    public event EventHandler<SnapshotScanResult> SnapshotScanFinished;
    public event EventHandler<FilesChangedEventArgs> FilesChanged;

    protected virtual void OnSnapshotComputing(OperationInfo e) {
      EventHandler<OperationInfo> handler = SnapshotScanStarted;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnSnapshotComputed(SnapshotScanResult e) {
      EventHandler<SnapshotScanResult> handler = SnapshotScanFinished;
      if (handler != null) handler(this, e);
    }

    protected virtual void OnFilesChanged(FilesChangedEventArgs e) {
      EventHandler<FilesChangedEventArgs> handler = FilesChanged;
      if (handler != null) handler(this, e);
    }

    private void FileRegistrationTrackerOnProjectListChanged(object o, IList<IProject> projects) {
      Logger.LogInfo("List of projects changed: Enqueuing a partial file system scan");
      _taskQueue.Enqueue(ProjectListChangedTaskId, () => {
        // Pass empty changes, as we don't know of any file system changes for
        // existing entries. For new entries, they don't exist in the snapshot,
        // so they will be read form disk
        var emptyChanges = new FullPathChanges(ArrayUtilities.EmptyList<PathChangeEntry>.Instance);
        RescanFileSystem(projects, emptyChanges);
      });
    }

    private void FileRegistrationTrackerOnFullRescanRequired(object sender, IList<IProject> projects) {
      Logger.LogInfo("List of projects changed dramatically: Enqueuing a full file system scan");
      // If we are queuing a task that requires rescanning the entire file system,
      // cancel existing tasks (should be only one really) to avoid wasting time
      _rescanCancellationTracker.CancelCurrent();

      _taskQueue.Enqueue(FullRescanRequiredTaskId, () => {
        RescanFileSystem(projects, null);
      });
    }

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      Logger.LogInfo("File change events: enqueuing an incremental file system rescan");
      _pathsChangedQueue.Enqueue(changes);
      _taskQueue.Enqueue(FlushPathsChangedQueueTaskId, FlushPathsChangedQueueTask);
    }

    private void DirectoryChangeWatcherOnError(Exception exception) {
      Logger.LogInfo("File change events error: queuing a full file system rescan");
      // Ingore all changes
      _pathsChangedQueue.DequeueAll();

      // Rescan all projects from scratch.
      _fileRegistrationTracker.Refresh();
    }

    private void FlushPathsChangedQueueTask() {
      var changes = _pathsChangedQueue
        .DequeueAll()
        .SelectMany(x => x)
        .ToList();

      var validationResult = new FileSystemChangesValidator(_fileSystemNameFactory, _fileSystem, _projectDiscovery)
        .ProcessPathsChangedEvent(changes);

      if (validationResult.NoChanges) {
        return;
      }

      if (validationResult.FileModificationsOnly) {
        OnFilesChanged(new FilesChangedEventArgs {
          ChangedFiles = validationResult.ModifiedFiles.ToReadOnlyCollection()
        });
        return;
      }

      if (validationResult.VariousFileChanges) {
        RescanFileSystem(_registeredProjects, validationResult.FileChanges);
        return;
      }

      if (validationResult.UnknownChanges) {
        _fileRegistrationTracker.Refresh();
        return;
      }

      Debug.Assert(false, "What kind of validation result is this?");
    }

    private void RescanFileSystem(IList<IProject> projects, FullPathChanges pathChanges /* may be null */) {
      _registeredProjects = projects;

      _operationProcessor.Execute(new OperationHandlers {

        OnBeforeExecute = info =>
          OnSnapshotComputing(info),

        OnError = (info, error) => {
          Logger.LogInfo("File system rescan error: {0}", error.Message);
          OnSnapshotComputed(new SnapshotScanResult {
            OperationInfo = info,
            Error = error
          });
        },

        Execute = info => {
          // Compute and assign new snapshot
          var oldSnapshot = _fileSystemSnapshot;
          var newSnapshot = BuildNewFileSystemSnapshot(projects, oldSnapshot, pathChanges, _rescanCancellationTracker.NewToken());
          // Update of new tree (assert calls are serialized).
          Debug.Assert(ReferenceEquals(oldSnapshot, _fileSystemSnapshot));
          _fileSystemSnapshot = newSnapshot;

          if (Logger.Info) {
            Logger.LogInfo("+++++++++++ Collected {0:n0} files in {1:n0} directories",
              newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountFileEntries(x.Directory)),
              newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x.Directory)));
          }

          // Post event
          OnSnapshotComputed(new SnapshotScanResult {
            OperationInfo = info,
            PreviousSnapshot = oldSnapshot,
            FullPathChanges = pathChanges,
            NewSnapshot = newSnapshot
          });
        }
      });
    }

    private FileSystemTreeSnapshot BuildNewFileSystemSnapshot(IList<IProject> projects, FileSystemTreeSnapshot oldSnapshot, FullPathChanges pathChanges, CancellationToken cancellationToken) {
      using (new TimeElapsedLogger("Computing snapshot delta from list of file changes")) {
        // file name factory
        var fileNameFactory = _fileSystemNameFactory;
        if (ReuseFileNameInstances) {
          if (_fileSystemSnapshot.ProjectRoots.Count > 0) {
            fileNameFactory = new FileSystemTreeSnapshotNameFactory(_fileSystemSnapshot, fileNameFactory);
          }
        }

        // Compute new snapshot
        var newSnapshot = _fileSystemSnapshotBuilder.Compute(
          fileNameFactory,
          oldSnapshot,
          pathChanges,
          projects,
          Interlocked.Increment(ref _version),
          cancellationToken);

        // Monitor all project roots for file changes.
        var newRoots = newSnapshot.ProjectRoots
          .Select(entry => entry.Directory.DirectoryName.FullPath);
        _directoryChangeWatcher.WatchDirectories(newRoots);

        return newSnapshot;
      }
    }

    private int CountFileEntries(DirectorySnapshot entry) {
      return
        entry.ChildFiles.Count +
        entry.ChildDirectories.Aggregate(0, (acc, x) => acc + CountFileEntries(x));
    }

    private int CountDirectoryEntries(DirectorySnapshot entry) {
      return
        1 +
        entry.ChildDirectories.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x));
    }

    /// <summary>
    /// Keeps track of a single cancellation token (the last one created) in a thread safe manner.
    /// </summary>
    private class CancellationTokenTracker {
      private CancellationTokenSource _currentTokenSource;

      /// <summary>
      /// Create a new token, replacing the previous one (which is now orphan)
      /// </summary>
      public CancellationToken NewToken() {
        var newSource = new CancellationTokenSource();
        Interlocked.Exchange(ref _currentTokenSource, newSource);
        return newSource.Token;
      }

      /// <summary>
      /// Cancel the last token created (if there was one)
      /// </summary>
      public void CancelCurrent() {
        var currentSource = Interlocked.Exchange(ref _currentTokenSource, null);
        if (currentSource != null) {
          currentSource.Cancel();
        }
      }
    }
  }
}
