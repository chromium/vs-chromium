// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using VsChromium.Core.Collections;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;
using VsChromium.Server.Cache;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Operations;
using VsChromium.Server.Projects;
using VsChromium.Server.Threads;

namespace VsChromium.Server.FileSystem {
  public class FileSystemSnapshotManager : IFileSystemSnapshotManager {
    private static readonly TaskId FlushPathsChangedQueueTaskId = new TaskId("FlushPathsChangedQueueTaskId");
    private static readonly TaskId FilesChangedTaskId = new TaskId("FilesChangedTaskId");
    private static readonly TaskId ProjectListChangedTaskId = new TaskId("ProjectListChangedTaskId");
    private static readonly TaskId FullRescanRequiredTaskId = new TaskId("FullRescanRequiredTaskId");
    private static readonly TaskId VerifyCachedTreesTaskId = new TaskId("VerifyCachedTreesTaskId");

    /// <summary>
    /// Delay before publishing the tree of a project from the index cache (phase 2): 0 (decision),
    /// the index of the last session is usable as soon as it is loaded, and verified in the
    /// background. A delay would let quick verifications complete first (no stale results shown).
    /// Hard-coded for now (a setting later): only 0 is implemented.
    /// </summary>
    public static readonly TimeSpan CachedTreePublishDelay = TimeSpan.Zero;

    /// <summary>
    /// Test only: delay (milliseconds) before verifying the trees of the index cache, so that tests
    /// can observe the "verifying" state.
    /// </summary>
    private const string TestVerifyDelayVariable = "VSCHROMIUM_TEST_VERIFY_DELAY_MS";

    private static readonly FullPathChanges EmptyChanges = new FullPathChanges(ArrayUtilities.EmptyList<PathChangeEntry>.Instance);

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
    private readonly IIndexCache _indexCache;

    /// <summary>
    /// The projects whose tree comes from the index cache and is not verified yet, with the files
    /// of their tree to read from disk (without contents in the cache).
    /// Access to this field is serialized through tasks executed on the <see cref="_longRunningFileSystemTaskQueue"/>
    /// </summary>
    private readonly Dictionary<FullPath, UnverifiedProject> _unverifiedProjects = new Dictionary<FullPath, UnverifiedProject>();

    private sealed class UnverifiedProject {
      /// <summary>The files of the tree of the cache without cached contents (read by the verification).</summary>
      public HashSet<FileName> FilesToReload;
      /// <summary>True if they are all larger than the files cached (not cached anyway).</summary>
      public bool FilesToReloadAreLarge;
    }

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


    public FileSystemSnapshotManager(
      IFileSystemNameFactory fileSystemNameFactory,
      IFileSystem fileSystem,
      IFileRegistrationTracker fileRegistrationTracker,
      IFileSystemSnapshotBuilder fileSystemSnapshotBuilder,
      IOperationProcessor operationProcessor,
      IProjectDiscovery projectDiscovery,
      IDirectoryChangeWatcherFactory directoryChangeWatcherFactory,
      ITaskQueueFactory taskQueueFactory,
      ILongRunningFileSystemTaskQueue longRunningFileSystemTaskQueue,
      IIndexCache indexCache = null) {
      _indexCache = indexCache;
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
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher(TimeSpan.FromSeconds(2));
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _directoryChangeWatcher.Error += DirectoryChangeWatcherOnError;
      _directoryChangeWatcher.Paused += DirectoryChangeWatcherOnPaused;
      _directoryChangeWatcher.Resumed += DirectoryChangeWatcherOnResumed;
      _directoryChangeWatcher.RescanRequired += DirectoryChangeWatcherOnRescanRequired;
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

    private void DirectoryChangeWatcherOnRescanRequired() {
      _taskExecutor.ExecuteAsync(token => {
        if (_isPaused) {
          return;
        }
        // Change notifications are not available (polling fallback): rescan everything.
        Logger.LogInfo("FileSystemSnapshotManager: Directory watcher requested a rescan (polling fallback)");
        _fileRegistrationTracker.RefreshAsync(FileRegistrationTrackerRefreshCompleted);
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
          // Phase 2 of the index cache: the new projects with a tree in the index cache are not
          // scanned (verified later). Not for full rescans ("Rebuild index").
          var cachedTrees = pathChanges == null ? null : LoadCachedTrees(projects, oldSnapshot, cancellationToken);
          var newSnapshot = BuildNewFileSystemSnapshot(projects, oldSnapshot, pathChanges,
            cachedTrees?.ToDictionary(x => x.Key, x => x.Value.Root), null, cancellationToken);
          UpdateUnverifiedProjects(projects, pathChanges, cachedTrees);
          newSnapshot = newSnapshot.WithCachedProjectRoots(_unverifiedProjects.Keys.ToList());
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

          // Note: After the event, so that the index of the cached trees is loaded first (same task
          // queue).
          ScheduleVerification();
        }
      });
    }

    /// <summary>
    /// The trees of the index cache of the projects not in <paramref name="oldSnapshot"/> (new
    /// projects), if their cache has one written with the current configuration of the project.
    /// </summary>
    private Dictionary<FullPath, CachedProjectTree> LoadCachedTrees(IList<IProject> projects, FileSystemSnapshot oldSnapshot,
                                                                   CancellationToken cancellationToken) {
      if (_indexCache == null || !_indexCache.IsEnabled)
        return null;
      Dictionary<FullPath, CachedProjectTree> result = null;
      foreach (var project in projects) {
        if (oldSnapshot.ProjectRoots.Any(x => x.Project.RootPath.Equals(project.RootPath)))
          continue;
        var tree = _indexCache.GetProjectCache(project.RootPath)?.TryLoadTree(project.VersionHash, _fileSystemNameFactory,
          project.RootPath, cancellationToken);
        if (tree != null) {
          (result ??= new Dictionary<FullPath, CachedProjectTree>())[project.RootPath] = tree;
        }
      }
      return result;
    }

    private void UpdateUnverifiedProjects(IList<IProject> projects, FullPathChanges pathChanges,
                                          Dictionary<FullPath, CachedProjectTree> cachedTrees) {
      // A full rescan verifies all the trees.
      if (pathChanges == null) {
        _unverifiedProjects.Clear();
      }
      if (cachedTrees != null) {
        foreach (var (root, tree) in cachedTrees) {
          _unverifiedProjects[root] = new UnverifiedProject {
            FilesToReload = new HashSet<FileName>(tree.FilesWithoutContents),
            FilesToReloadAreLarge = tree.FilesWithoutContentsAreLarge,
          };
        }
      }
      // Projects not registered anymore
      foreach (var root in _unverifiedProjects.Keys.ToList()) {
        if (!projects.Any(x => x.RootPath.Equals(root))) {
          _unverifiedProjects.Remove(root);
        }
      }
    }

    private void ScheduleVerification() {
      if (_unverifiedProjects.Count > 0) {
        _longRunningFileSystemTaskQueue.Enqueue(VerifyCachedTreesTaskId, VerifyCachedTrees);
      }
    }

    /// <summary>
    /// Verifies the trees of the index cache in use (phase 2): scans the projects (the directory
    /// watcher already runs: changes during the scan are queued and applied after), compares the
    /// scan with the cached tree, and publishes the scanned tree with the differences as synthetic
    /// changes (<see cref="SnapshotDiff"/>): the incremental update of the index re-reads the files
    /// with another time or size and the files without cached contents, so that the index is the
    /// same as the index of a cold load.
    /// </summary>
    private void VerifyCachedTrees(CancellationToken cancellationToken) {
      if (_unverifiedProjects.Count == 0 || _isPaused)
        return;
      if (int.TryParse(Environment.GetEnvironmentVariable(TestVerifyDelayVariable), out var delayMs) && delayMs > 0) {
        cancellationToken.WaitHandle.WaitOne(delayMs);
        cancellationToken.ThrowIfCancellationRequested();
      }

      var roots = new HashSet<FullPath>(_unverifiedProjects.Keys);
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info =>
          OnSnapshotScanStarted(info),

        OnError = (info, error) => {
          if (!error.IsCanceled()) {
            Logger.LogError(error, "Error verifying the trees of the index cache");
          }
          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            Error = error
          });
        },

        Execute = info => {
          var oldSnapshot = _currentSnapshot;
          var newSnapshot = BuildNewFileSystemSnapshot(_registeredProjects, oldSnapshot, EmptyChanges, null, roots,
            cancellationToken);
          var changes = new List<PathChangeEntry>();
          // True if the verified index is the same as the cache: same trees, and the files read
          // from disk are not cached anyway (larger than 512 KB): nothing to save.
          var cacheUnchanged = true;
          using (new TimeElapsedLogger("Comparing the trees of the index cache with the file system", cancellationToken, InfoLogger.Instance)) {
            foreach (var root in roots) {
              var oldRoot = oldSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(root));
              var newRoot = newSnapshot.ProjectRoots.FirstOrDefault(x => x.Project.RootPath.Equals(root));
              if (oldRoot != null && newRoot != null) {
                var project = _unverifiedProjects[root];
                var projectChanges = SnapshotDiff.Compute(root, oldRoot.Directory, newRoot.Directory, project.FilesToReload,
                  cancellationToken);
                changes.AddRange(projectChanges);
                cacheUnchanged &= project.FilesToReloadAreLarge &&
                  projectChanges.Count == project.FilesToReload.Count &&
                  SnapshotDiff.Compute(root, oldRoot.Directory, newRoot.Directory, null, cancellationToken).Count == 0;
              }
            }
          }
          foreach (var root in roots) {
            _unverifiedProjects.Remove(root);
          }
          newSnapshot = newSnapshot.WithCachedProjectRoots(_unverifiedProjects.Keys.ToList());
          Invariants.Assert(ReferenceEquals(oldSnapshot, _currentSnapshot));
          _currentSnapshot = newSnapshot;
          Logger.LogInfo("FileSystemSnapshotManager: Verified the trees of the index cache: {0:n0} changes " +
                         "({1:n0} created, {2:n0} deleted, {3:n0} changed).",
            changes.Count,
            changes.Count(x => x.ChangeKind == PathChangeKind.Created),
            changes.Count(x => x.ChangeKind == PathChangeKind.Deleted),
            changes.Count(x => x.ChangeKind == PathChangeKind.Changed));

          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            PreviousSnapshot = oldSnapshot,
            FullPathChanges = new FullPathChanges(changes),
            NewSnapshot = newSnapshot,
            CacheUnchanged = cacheUnchanged,
          });
        }
      });
    }

    private FileSystemSnapshot BuildNewFileSystemSnapshot(IList<IProject> projects,
      FileSystemSnapshot oldSnapshot, FullPathChanges pathChanges,
      IReadOnlyDictionary<FullPath, DirectorySnapshot> precomputedRoots, ISet<FullPath> fullScanRoots,
      CancellationToken cancellationToken) {

      using (new TimeElapsedLogger("FileSystemSnapshotManager: Computing snapshot delta from list of file changes", cancellationToken, InfoLogger.Instance)) {
        // Compute new snapshot
        var newSnapshot = _fileSystemSnapshotBuilder.Compute(
          _fileSystemNameFactory,
          oldSnapshot,
          pathChanges,
          projects,
          Interlocked.Increment(ref _version),
          cancellationToken,
          precomputedRoots,
          fullScanRoots);

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
