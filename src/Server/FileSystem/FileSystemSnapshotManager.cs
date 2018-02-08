// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IFileSystemSnapshotManager))]
  public class FileSystemSnapshotManager : IFileSystemSnapshotManager {
    private static readonly TaskId FlushPathsChangedQueueTaskId = new TaskId("FlushPathsChangedQueueTaskId");
    private static readonly TaskId FilesChangedTaskId = new TaskId("FilesChangedTaskId");
    private static readonly TaskId ProjectListChangedTaskId = new TaskId("ProjectListChangedTaskId");
    private static readonly TaskId FullRescanRequiredTaskId = new TaskId("FullRescanRequiredTaskId");

    private readonly IProjectDiscovery _projectDiscovery;
    private readonly IDirectoryChangeWatcher _directoryChangeWatcher;
    private readonly IFileSystemNameFactory _fileSystemNameFactory;
    private readonly IFileSystem _fileSystem;
    private readonly IFileSystemSnapshotBuilder _fileSystemSnapshotBuilder;
    private readonly IOperationProcessor _operationProcessor;
    private readonly ITaskQueue _longRunningFileSystemTaskQueue;
    private readonly ITaskQueue _flushPathChangesTaskQueue;
    private readonly ITaskQueue _taskExecutor;

    private readonly ConcurrentBufferQueue<IList<PathChangeEntry>> _pathsChangedQueue =
      new ConcurrentBufferQueue<IList<PathChangeEntry>>();

    private readonly IFileRegistrationTracker _fileRegistrationTracker;

    /// <summary>
    /// Access to this field is serialized through tasks executed on the <see cref="_longRunningFileSystemTaskQueue"/>
    /// </summary>
    private IList<IProject> _registeredProjects = new List<IProject>();

    /// <summary>
    /// Access to this field is serialized through tasks executed on the <see cref="_longRunningFileSystemTaskQueue"/>
    /// </summary>
    private FileSystemSnapshot _currentSnapshot;

    private int _version;

    /// <summary>
    /// <code>true</code> if the file system watchers are currently not active.
    /// </summary>
    private bool _isPaused;


    [ImportingConstructor]
    public FileSystemSnapshotManager(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IFileRegistrationTracker fileRegistrationTracker,
      IFileSystemSnapshotBuilder fileSystemSnapshotBuilder,
      IOperationProcessor operationProcessor,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      ITaskQueueFactory taskQueueFactory,
      ILongRunningFileSystemTaskQueue longRunningFileSystemTaskQueue) {
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _fileSystemSnapshotBuilder = fileSystemSnapshotBuilder;
      _operationProcessor = operationProcessor;
      _projectDiscovery = projectDiscovery;
      _longRunningFileSystemTaskQueue = longRunningFileSystemTaskQueue;
      _fileRegistrationTracker = fileRegistrationTracker;

      _flushPathChangesTaskQueue = taskQueueFactory.CreateQueue("FileSystemSnapshotManager Path Changes Task Queue");
      _taskExecutor = taskQueueFactory.CreateQueue("FileSystemSnapshotManager State Change Task Queue");
      _fileRegistrationTracker.ProjectListChanged += FileRegistrationTrackerOnProjectListChanged;
      _currentSnapshot = FileSystemSnapshot.Empty;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher(TimeSpan.FromSeconds(60));
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _directoryChangeWatcher.Error += DirectoryChangeWatcherOnError;
      _directoryChangeWatcher.Paused += DirectoryChangeWatcherOnPaused;
      _directoryChangeWatcher.Resumed += DirectoryChangeWatcherOnResumed;
    }

    public FileSystemSnapshot CurrentSnapshot {
      get { return _currentSnapshot; }
    }

    public void Pause() {
      _taskExecutor.ExecuteAsync(token => {
        // "OnPause" event will be fired by the directory watcher
        _directoryChangeWatcher.Pause();
      });
    }

    public void Resume() {
      _taskExecutor.ExecuteAsync(token => {
        // "OnResume" event will be fired by the directory watcher
        _directoryChangeWatcher.Resume();
      });
    }

    public void Refresh() {
      _taskExecutor.ExecuteAsync(token => {
        _longRunningFileSystemTaskQueue.CancelAll();
        _fileRegistrationTracker.RefreshAsync(FileRegistrationTrackerRefreshCompleted);
      });
    }

    public event EventHandler<OperationInfo> SnapshotScanStarted;
    public event EventHandler<SnapshotScanResult> SnapshotScanFinished;
    public event EventHandler<FilesChangedEventArgs> FilesChanged;
    public event EventHandler<FileSystemWatchPausedEventArgs> FileSystemWatchPaused;
    public event EventHandler FileSystemWatchResumed;

    protected virtual void OnSnapshotScanStarted(OperationInfo e) {
      SnapshotScanStarted?.Invoke(this, e);
    }

    protected virtual void OnSnapshotScanFinished(SnapshotScanResult e) {
      SnapshotScanFinished?.Invoke(this, e);
    }

    protected virtual void OnFilesChanged(FilesChangedEventArgs e) {
      FilesChanged?.Invoke(this, e);
    }

    protected virtual void OnFileSystemWatchPaused(FileSystemWatchPausedEventArgs e) {
      FileSystemWatchPaused?.Invoke(this, e);
    }

    protected virtual void OnFileSystemWatchResumed() {
      FileSystemWatchResumed?.Invoke(this, EventArgs.Empty);
    }

    private void FileRegistrationTrackerOnProjectListChanged(object sender, ProjectsEventArgs e) {
      _taskExecutor.ExecuteAsync(token => {
        Logger.LogInfo("FileSystemSnapshotManager: List of projects has changed, enqueuing a partial file system scan");

        // If we are queuing a task that requires rescanning the entire file system,
        // cancel existing tasks (should be only one really) to avoid wasting time
        _longRunningFileSystemTaskQueue.CancelAll();

        if (!_isPaused) {
          _longRunningFileSystemTaskQueue.Enqueue(ProjectListChangedTaskId, cancellationToken => {
            // Pass empty changes, as we don't know of any file system changes for
            // existing entries. For new entries, they don't exist in the snapshot,
            // so they will be read form disk
            var emptyChanges = new FullPathChanges(ArrayUtilities.EmptyList<PathChangeEntry>.Instance);
            RescanFileSystem(e.Projects, emptyChanges, cancellationToken);
          });
        }
      });
    }

    private void FileRegistrationTrackerRefreshCompleted(IList<IProject> projects) {
      _taskExecutor.ExecuteAsync(token => {
        Logger.LogInfo("FileSystemSnapshotManager: List of projects has been refreshed, enqueuing a full file system scan");

        // If we are queuing a task that requires rescanning the entire file system,
        // cancel existing tasks (should be only one really) to avoid wasting time
        _longRunningFileSystemTaskQueue.CancelAll();

        // Note: Don't check for "paused" state, as we arrive here only for explicit
        // refresh.
        _longRunningFileSystemTaskQueue.Enqueue(FullRescanRequiredTaskId, cancellationToken => {
          RescanFileSystem(projects, null, cancellationToken);
        });
      });
    }

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      _taskExecutor.ExecuteAsync(token => {
        if (!_isPaused) {
          _pathsChangedQueue.Enqueue(changes);
          _flushPathChangesTaskQueue.Enqueue(FlushPathsChangedQueueTaskId, FlushPathsChangedQueueTask);
        }
      });
    }

    private void DirectoryChangeWatcherOnError(Exception exception) {
      _taskExecutor.ExecuteAsync(token => {
        Logger.LogInfo("FileSystemSnapshotManager: Directory watcher error received, entering pause mode");
        // Note: No need to stop the directory watcher, it paused itself
        _isPaused = true;

        // Ingore all changes
        _pathsChangedQueue.DequeueAll();
        _longRunningFileSystemTaskQueue.CancelAll();
        OnFileSystemWatchPaused(new FileSystemWatchPausedEventArgs { IsError = true });
      });
    }

    private void DirectoryChangeWatcherOnPaused() {
      _taskExecutor.ExecuteAsync(token => {
        Logger.LogInfo("FileSystemSnapshotManager: Directory watcher entered pause mode, entering pause mode");
        _isPaused = true;

        // Ingore all changes
        _pathsChangedQueue.DequeueAll();
        _longRunningFileSystemTaskQueue.CancelAll();
        OnFileSystemWatchPaused(new FileSystemWatchPausedEventArgs { IsError = false });
      });
    }

    private void DirectoryChangeWatcherOnResumed() {
      _taskExecutor.ExecuteAsync(token => {
        Logger.LogInfo("FileSystemSnapshotManager: Directory watcher has resumed, leaving pause mode");
        _isPaused = false;

        // Ingore all changes
        _pathsChangedQueue.DequeueAll();
        _longRunningFileSystemTaskQueue.CancelAll();
        _fileRegistrationTracker.RefreshAsync(FileRegistrationTrackerRefreshCompleted);
        OnFileSystemWatchResumed();
      });
    }

    private void FlushPathsChangedQueueTask(CancellationToken cancellationToken) {
      cancellationToken.ThrowIfCancellationRequested();
      ProcessPendingPathChanges();
    }

    private void ProcessPendingPathChanges() {
      var changes = _pathsChangedQueue
        .DequeueAll()
        .SelectMany(x => x)
        .ToList();

      var validationResult = new FileSystemChangesValidator(_fileSystemNameFactory, _fileSystem, _projectDiscovery)
        .ProcessPathsChangedEvent(changes);

      switch (validationResult.Kind) {
        case FileSystemValidationResultKind.NoChanges:
          return;

        case FileSystemValidationResultKind.FileModificationsOnly:
          OnFilesChanged(new FilesChangedEventArgs {
            FileSystemSnapshot = _currentSnapshot,
            ChangedFiles = validationResult.ModifiedFiles.ToReadOnlyCollection()
          });
          return;

        case FileSystemValidationResultKind.VariousFileChanges:
          _longRunningFileSystemTaskQueue.Enqueue(FilesChangedTaskId, cancellationToken => {
            RescanFileSystem(_registeredProjects, validationResult.FileChanges, cancellationToken);
          });
          return;

        case FileSystemValidationResultKind.UnknownChanges:
          _fileRegistrationTracker.RefreshAsync(FileRegistrationTrackerRefreshCompleted);
          return;

        default:
          Invariants.Assert(false, "What kind of validation result is this?");
          return;
      }
    }

    private void RescanFileSystem(IList<IProject> projects, FullPathChanges pathChanges /* may be null */,
      CancellationToken cancellationToken) {
      _registeredProjects = projects;

      _operationProcessor.Execute(new OperationHandlers {

        OnBeforeExecute = info =>
          OnSnapshotScanStarted(info),

        OnError = (info, error) => {
          if (!error.IsCanceled()) {
            Logger.LogError(error, "File system rescan error");
          }
          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            Error = error
          });
        },

        Execute = info => {
          // Compute and assign new snapshot
          var oldSnapshot = _currentSnapshot;
          var newSnapshot = BuildNewFileSystemSnapshot(projects, oldSnapshot, pathChanges, cancellationToken);
          // Update of new tree (assert calls are serialized).
          Invariants.Assert(ReferenceEquals(oldSnapshot, _currentSnapshot));
          _currentSnapshot = newSnapshot;

          if (Logger.IsInfoEnabled) {
            Logger.LogInfo("FileSystemSnapshotManager: New snapshot contains {0:n0} files in {1:n0} directories",
              newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountFileEntries(x.Directory)),
              newSnapshot.ProjectRoots.Aggregate(0, (acc, x) => acc + CountDirectoryEntries(x.Directory)));
          }

          // Post event
          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            PreviousSnapshot = oldSnapshot,
            FullPathChanges = pathChanges,
            NewSnapshot = newSnapshot
          });
        }
      });
    }

    private FileSystemSnapshot BuildNewFileSystemSnapshot(IList<IProject> projects,
      FileSystemSnapshot oldSnapshot, FullPathChanges pathChanges, CancellationToken cancellationToken) {

      using (new TimeElapsedLogger("FileSystemSnapshotManager: Computing snapshot delta from list of file changes", cancellationToken, InfoLogger.Instance)) {
        // Compute new snapshot
        var newSnapshot = _fileSystemSnapshotBuilder.Compute(
          _fileSystemNameFactory,
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

    public static int CountFileEntries(DirectorySnapshot entry) {
      var result = entry.ChildFiles.Count;
      foreach (var x in entry.ChildDirectories.ToForeachEnum()) {
        result += CountFileEntries(x);
      }
      return result;
    }

    public static int CountDirectoryEntries(DirectorySnapshot entry) {
      var result = 1;
      foreach (var x in entry.ChildDirectories.ToForeachEnum()) {
        result += CountDirectoryEntries(x);
      }
      return result;
    }
  }
}