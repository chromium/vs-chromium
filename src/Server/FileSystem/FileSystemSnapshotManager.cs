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
using VsChromium.Server.Diagnostics;
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
      /// <summary>The change journal checkpoint of the tree of the cache (null if none).</summary>
      public ChangeJournalCheckpoint Checkpoint;
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
      IIndexCache indexCache = null,
      IChangeJournal changeJournal = null) {
      _indexCache = indexCache;
      if (indexCache != null && changeJournal != null) {
        // The journal self-check of the saves of the index cache: the differences it finds are
        // missed changes, applied as changes of the directory watcher.
        indexCache.CheckpointProvider = new ChangeJournalSelfCheck(changeJournal, fileSystem, DirectoryChangeWatcherOnPathsChanged).Run;
        // The idle advance of the checkpoints: only on the current snapshot, when it is verified and
        // the file system watch is active (not paused).
        indexCache.CheckpointAdvanceAllowed = snapshot =>
          !_isPaused && ReferenceEquals(snapshot, _currentSnapshot) && !snapshot.IsFromCache;
      }
      _fileSystemNameFactory = fileSystemNameFactory;
      _fileSystem = fileSystem;
      _fileSystemSnapshotBuilder = fileSystemSnapshotBuilder;
      _operationProcessor = operationProcessor;
      _projectDiscovery = projectDiscovery;
      _longRunningFileSystemTaskQueue = longRunningFileSystemTaskQueue;
      _fileRegistrationTracker = fileRegistrationTracker;

      _flushPathChangesTaskQueue = taskQueueFactory.CreateQueue("FileSystemSnapshotManager Path Changes Task Queue");
      _taskExecutor = taskQueueFactory.CreateQueue("FileSystemSnapshotManager State Change Task Queue");
      _symLinkTargetsTaskQueue = taskQueueFactory.CreateQueue("FileSystemSnapshotManager Symbolic Link Targets Task Queue");
      _fileRegistrationTracker.ProjectListChanged += FileRegistrationTrackerOnProjectListChanged;
      _currentSnapshot = FileSystemSnapshot.Empty;
      _directoryChangeWatcher = directoryChangeWatcherFactory.CreateWatcher(TimeSpan.FromSeconds(2));
      _directoryChangeWatcher.PathsChanged += DirectoryChangeWatcherOnPathsChanged;
      _directoryChangeWatcher.Error += DirectoryChangeWatcherOnError;
      _directoryChangeWatcher.Paused += DirectoryChangeWatcherOnPaused;
      _directoryChangeWatcher.Resumed += DirectoryChangeWatcherOnResumed;
      _directoryChangeWatcher.RescanRequired += DirectoryChangeWatcherOnRescanRequired;
      _symLinkTargetWatcher = new SymLinkTargetWatcher(fileSystem, DirectoryChangeWatcherOnPathsChanged);
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

    public void RebuildProject(FullPath projectRoot) {
      _taskExecutor.ExecuteAsync(token => {
        _longRunningFileSystemTaskQueue.Enqueue(new TaskId("RebuildProjectTaskId:" + projectRoot.Value),
          cancellationToken => RebuildProjectTask(projectRoot, cancellationToken));
      });
    }

    /// <summary>
    /// Rescans a project (the other projects keep their tree) and publishes the new tree with its
    /// root as a created directory: the update of the index reads all the files of the project
    /// again (the files of the other projects are unchanged).
    /// </summary>
    private void RebuildProjectTask(FullPath projectRoot, CancellationToken cancellationToken) {
      if (!_registeredProjects.Any(x => x.RootPath.Equals(projectRoot)))
        return;
      _operationProcessor.Execute(new OperationHandlers {
        OnBeforeExecute = info =>
          OnSnapshotScanStarted(info),

        OnError = (info, error) => {
          if (!error.IsCanceled()) {
            Logger.LogError(error, "Error rebuilding the project \"{0}\"", projectRoot);
          }
          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            Error = error
          });
        },

        Execute = info => {
          Logger.LogInfo("FileSystemSnapshotManager: Rebuilding the project \"{0}\".", projectRoot);
          var oldSnapshot = _currentSnapshot;
          var newSnapshot = BuildNewFileSystemSnapshot(_registeredProjects, oldSnapshot, EmptyChanges,
            new SnapshotComputeOptions { FullScanRoots = new HashSet<FullPath> { projectRoot } }, cancellationToken);
          // The scan verifies the tree of the cache of the project, if any.
          _unverifiedProjects.Remove(projectRoot);
          newSnapshot = newSnapshot.WithCachedProjectRoots(_unverifiedProjects.Keys.ToList());
          Invariants.Assert(ReferenceEquals(oldSnapshot, _currentSnapshot));
          _currentSnapshot = newSnapshot;

          OnSnapshotScanFinished(new SnapshotScanResult {
            OperationInfo = info,
            PreviousSnapshot = oldSnapshot,
            FullPathChanges = new FullPathChanges(new[] {
              new PathChangeEntry(projectRoot, RelativePath.Empty, PathChangeKind.Created, PathKind.Directory),
            }),
            NewSnapshot = newSnapshot,
          });
          UpdateSymLinkTargets(newSnapshot);
        }
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

    /// <summary>
    /// The symbolic links of the current snapshot by target (see <see cref="SymLinkTargetMap"/>):
    /// the changes of the targets are changes of the links.
    /// </summary>
    private volatile SymLinkTargetMap _symLinkTargets = SymLinkTargetMap.Empty;
    /// <summary>The project trees of <see cref="_symLinkTargets"/> (rebuilt when they change).</summary>
    private IReadOnlyList<DirectorySnapshot> _symLinkTargetsRoots = Array.Empty<DirectorySnapshot>();
    /// <summary>The links created, deleted or replaced since <see cref="_symLinkTargets"/> (their target is resolved again).</summary>
    private readonly HashSet<string> _invalidatedSymLinks = new HashSet<string>(PathHelpers.FileNameComparer);
    private readonly SymLinkTargetWatcher _symLinkTargetWatcher;

    /// <summary>
    /// Rebuilds the map of the symbolic links (and the watchers of their targets outside of the
    /// projects) if the trees of the projects changed. Not for the trees of the index cache not
    /// verified yet: done after their verification (it resolves the links: no disk access).
    /// </summary>
    private void UpdateSymLinkTargets(FileSystemSnapshot snapshot) {
      if (snapshot.IsFromCache)
        return;
      // Note: In the background (~0.2 s on a 640k directory tree with 45k links): not on the path
      // of the index updates. The changes are expanded with the previous map meanwhile.
      _symLinkTargetsTaskQueue.Enqueue(SymLinkTargetsTaskId, token => {
        var current = _currentSnapshot;
        if (current.IsFromCache)
          return;
        lock (_symLinkTargetsLock) {
          UpdateSymLinkTargetsLocked(current);
        }
      });
    }

    private static readonly TaskId SymLinkTargetsTaskId = new TaskId("SymLinkTargetsTaskId");
    private readonly ITaskQueue _symLinkTargetsTaskQueue;
    private readonly object _symLinkTargetsLock = new object();

    private void UpdateSymLinkTargetsLocked(FileSystemSnapshot snapshot) {
      var roots = snapshot.ProjectRoots.Select(x => x.Directory).ToList();
      HashSet<string> invalidated;
      lock (_invalidatedSymLinks) {
        // Note: Rebuilt if the trees changed, or if links were replaced (e.g. a file link deleted
        // and created again: a "changed" file, the tree is the same).
        if (_invalidatedSymLinks.Count == 0 && roots.Count == _symLinkTargetsRoots.Count &&
            roots.Zip(_symLinkTargetsRoots).All(x => ReferenceEquals(x.First, x.Second)))
          return;
        invalidated = new HashSet<string>(_invalidatedSymLinks, PathHelpers.FileNameComparer);
        _invalidatedSymLinks.Clear();
      }
      var stopwatch = System.Diagnostics.Stopwatch.StartNew();
      var map = SymLinkTargetMap.Build(snapshot, _symLinkTargets, invalidated, (path, allowCached) => _fileSystem.ResolveSymLinkTarget(path, allowCached));
      _symLinkTargets = map;
      _symLinkTargetsRoots = roots;
      var directories = UpdateSymLinkTargetWatchersLocked();
      if (map.FileLinkCount + map.DirectoryLinkCount > 0) {
        Logger.LogInfo("FileSystemSnapshotManager: {0:n0} file and {1:n0} directory symbolic links ({2:n0} target directories " +
                       "outside of the projects watched), mapped in {3:n0} ms.", map.FileLinkCount, map.DirectoryLinkCount, directories.Count,
          stopwatch.ElapsedMilliseconds);
      }
    }

    /// <summary>
    /// The project roots whose targets of symbolic links outside of the project roots are watched
    /// ("project/setOptions", kept for the lifetime of the server).
    /// </summary>
    private readonly HashSet<FullPath> _watchExternalLinkTargets = new HashSet<FullPath>();

    public void SetWatchExternalLinkTargets(FullPath projectRoot, bool enabled) {
      lock (_watchExternalLinkTargets) {
        if (!(enabled ? _watchExternalLinkTargets.Add(projectRoot) : _watchExternalLinkTargets.Remove(projectRoot)))
          return;
      }
      Logger.LogInfo("FileSystemSnapshotManager: targets of symbolic links outside of \"{0}\" {1}.", projectRoot,
        enabled ? "watched" : "not watched anymore");
      // Note: The map is unchanged: only the watchers.
      _symLinkTargetsTaskQueue.Enqueue(SymLinkTargetWatchersTaskId, token => {
        lock (_symLinkTargetsLock) {
          UpdateSymLinkTargetWatchersLocked();
        }
      });
    }

    private bool IsWatchingExternalLinkTargets(FullPath projectRoot) {
      lock (_watchExternalLinkTargets) {
        return _watchExternalLinkTargets.Contains(projectRoot);
      }
    }

    private static readonly TaskId SymLinkTargetWatchersTaskId = new TaskId("SymLinkTargetWatchersTaskId");

    /// <summary>Watches the targets outside of the projects of the links of the projects with the option.</summary>
    private IReadOnlyList<(FullPath Directory, bool Recursive)> UpdateSymLinkTargetWatchersLocked() {
      var directories = _symLinkTargets.GetTargetDirectoriesToWatch(IsWatchingExternalLinkTargets);
      _symLinkTargetWatcher.Update(directories);
      return directories;
    }

    public SymLinkStatistics GetSymLinkStatistics(FullPath projectRoot) {
      var statistics = _symLinkTargets.GetStatistics(projectRoot);
      statistics.ExternalTargetsWatched = IsWatchingExternalLinkTargets(projectRoot);
      if (statistics.ExternalTargetsWatched && statistics.ExternalTargetDirectories.Count > 0) {
        var watched = new HashSet<(FullPath, bool)>(_symLinkTargetWatcher.WatchedDirectories);
        statistics.WatcherCount = statistics.ExternalTargetDirectories.Count(x => watched.Contains(x));
      }
      return statistics;
    }

    /// <summary>
    /// Adds the changes of the paths of the symbolic links that see the changed paths (targets), and
    /// records the links whose own path changed (their target is resolved again).
    /// </summary>
    private IList<PathChangeEntry> ExpandSymLinkChanges(IList<PathChangeEntry> changes) {
      var map = _symLinkTargets;
      foreach (var change in changes) {
        // Note: A link replaced (deleted and created again) can be a "changed" file.
        var links = change.ChangeKind == PathChangeKind.Changed
          ? (map.IsLink(change.Path) ? new[] { SymLinkTargetMap.Normalize(change.Path.Value) } : Array.Empty<string>())
          : map.LinksAtOrUnder(change.Path);
        foreach (var link in links) {
          lock (_invalidatedSymLinks) {
            _invalidatedSymLinks.Add(link);
          }
        }
      }
      return map.ExpandAll(changes);
    }

    private void DirectoryChangeWatcherOnPathsChanged(IList<PathChangeEntry> changes) {
      changes = ExpandSymLinkChanges(changes);
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
          // Links replaced (the tree is the same): their targets are resolved again.
          UpdateSymLinkTargets(_currentSnapshot);
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
            cachedTrees == null ? null : new SnapshotComputeOptions {
              PrecomputedRoots = cachedTrees.ToDictionary(x => x.Key, x => (x.Value.Root, x.Value.Checkpoint)),
            }, cancellationToken);
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

          UpdateSymLinkTargets(newSnapshot);

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
            Checkpoint = tree.Checkpoint,
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
          // From the change journal if possible (catch-up), otherwise by a full scan.
          var options = new SnapshotComputeOptions {
            FullScanRoots = roots,
            VerifyRoots = roots.ToDictionary(x => x, x => _unverifiedProjects[x].Checkpoint),
          };
          var newSnapshot = BuildNewFileSystemSnapshot(_registeredProjects, oldSnapshot, EmptyChanges, options,
            cancellationToken);
          foreach (var (root, verification) in options.Verifications) {
            Logger.LogInfo("FileSystemSnapshotManager: Verification of the tree of the index cache of \"{0}\": {1}.", root,
              verification);
            ProjectOperations.RecordVerification(root, verification);
          }
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
                // Note: The cache is saved if its change journal checkpoint must be updated (e.g.
                // after a full scan, so that the next start uses the journal).
                cacheUnchanged &= !(options.Verifications.TryGetValue(root, out var verification) && verification.CheckpointNeedsSave);
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
          UpdateSymLinkTargets(newSnapshot);
        }
      });
    }

    private FileSystemSnapshot BuildNewFileSystemSnapshot(IList<IProject> projects,
      FileSystemSnapshot oldSnapshot, FullPathChanges pathChanges, SnapshotComputeOptions options,
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
          options);

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
