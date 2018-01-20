// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    /// <summary>
    /// Record the last 100 change notification, for debugging purpose only.
    /// </summary>
    private static readonly PathChangeRecorder GlobalChangeRecorder = new PathChangeRecorder();
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TimeSpan? _autoRestartDelay;
    private readonly TimeSpan _autoRestartObservePeriod = TimeSpan.FromSeconds(10);
    private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
    private readonly PollingDelayPolicy _pathChangesPolling;
    private readonly PollingDelayPolicy _simplePathChangesPolling;
    private readonly PollingDelayPolicy _checkRootsPolling;
    private readonly BoundedOperationLimiter _logLimiter = new BoundedOperationLimiter(10);

    /// <summary>
    /// Dictionary of watchers, one per root directory path.
    /// </summary>
    private readonly Dictionary<FullPath, DirectoryWatcherhEntry> _watchers = new Dictionary<FullPath, DirectoryWatcherhEntry>();
    private readonly object _watchersLock = new object();

    /// <summary>
    /// Dictionary of file change events, per path.
    /// </summary>
    private Dictionary<FullPath, PathChangeEntry> _changedPaths = new Dictionary<FullPath, PathChangeEntry>();
    private readonly object _changedPathsLock = new object();

    /// <summary>
    /// The polling and event posting thread.
    /// </summary>
    private readonly TimeSpan _pollingThreadTimeout = TimeSpan.FromSeconds(1.0);
    private Thread _pollingThread;

    /// <summary>
    /// Our current state which changes when we are started, stopped, run into errors with file system watcher, etc.
    /// This state allows reacting to external operations and events specifically for each state.
    /// </summary>
    private State _state;
    /// <summary>
    /// The lock to protect access to <see cref="_state"/>
    /// </summary>
    private readonly object _stateLock = new object();

    public DirectoryChangeWatcher(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider, TimeSpan? autoRestartDelay) {
      _fileSystem = fileSystem;
      _dateTimeProvider = dateTimeProvider;
      _autoRestartDelay = autoRestartDelay;
      _simplePathChangesPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(10.0));
      _pathChangesPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(60.0));
      _checkRootsPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(15.0), TimeSpan.FromSeconds(60.0));
      _state = new RunningState(new SharedState(this));
    }

    public void WatchDirectories(IEnumerable<FullPath> directories) {
      lock (_stateLock) {
        _state = _state.OnWatchDirectories(directories);
        _state.OnStateActive();
      }
    }

    public void Pause() {
      lock (_stateLock) {
        _state = _state.OnPause();
        _state.OnStateActive();
      }
    }

    public void Resume() {
      lock (_stateLock) {
        _state = _state.OnResume();
        _state.OnStateActive();
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;
    public event Action<Exception> Error;
    public event Action Paused;
    public event Action Resumed;

    protected virtual void OnError(Exception obj) {
      Error?.Invoke(obj);
    }

    protected virtual void OnPathsChanged(IList<PathChangeEntry> changes) {
      PathsChanged?.Invoke(changes);
    }

    protected virtual void OnPaused() {
      Paused?.Invoke();
    }

    protected virtual void OnResumed() {
      Resumed?.Invoke();
    }

    private void WatcherOnError(object sender, ErrorEventArgs args) {
      Logger.WrapActionInvocation(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherErrorEvent(sender, args);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs args, PathKind pathKind) {
      Logger.WrapActionInvocation(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileChangedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs args, PathKind pathKind) {
      Logger.WrapActionInvocation(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileCreatedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs args, PathKind pathKind) {
      Logger.WrapActionInvocation(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileDeletedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs args, PathKind pathKind) {
      Logger.WrapActionInvocation(() => {
        lock (_stateLock) {
          _state = _state.OnWatcherFileRenamedEvent(sender, args, pathKind);
          _state.OnStateActive();
        }
      });
    }

    [SuppressMessage("ReSharper", "UnusedParameter.Local")]
    private static void LogPathForDebugging(string path, PathChangeKind kind, PathKind pathKind) {
#if false
      var pathToLog = @"";
      if (SystemPathComparer.Instance.IndexOf(path, pathToLog, 0, path.Length) == 0) {
        Logger.LogInfo("*************************** {0}: {1}-{2} *******************", path, kind, pathKind);
      }
#endif
    }

    private void ThreadLoop() {
      Logger.LogInfo("Starting directory change notification monitoring thread.");
      try {
        while (true) {
          _eventReceived.WaitOne(_pollingThreadTimeout);
          lock (_stateLock) {
            _state = _state.OnPolling();
            _state.OnStateActive();
          }
        }
      } catch (Exception e) {
        Logger.LogError(e, "Error in DirectoryChangeWatcher.");
      }
    }

    /// <summary>
    /// Filter, remove and post events for all changes in <paramref
    /// name="paths"/> that match <paramref name="predicate"/>
    /// </summary>
    private void PostPathsChangedEvents(IDictionary<FullPath, PathChangeEntry> paths, Func<PathChangeKind, bool> predicate) {
      RemoveIgnorableEvents(paths);

      var changes = paths
        .Where(x => predicate(x.Value.ChangeKind))
        .Select(x => x.Value)
        .ToList();
      if (changes.Count == 0)
        return;

      changes.ForAll(x => paths.Remove(x.Path));
      if (changes.Count > 0) {
        OnPathsChanged(changes);
      }
    }

    private bool IncludeChange(PathChangeEntry entry) {
      // Ignore changes for files that have been created then deleted
      if (entry.ChangeKind == PathChangeKind.None)
        return false;

      return true;
    }

    private IDictionary<FullPath, PathChangeEntry> DequeueChangedPathsEvents() {
      // Copy current changes into temp and reset to empty collection.
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<FullPath, PathChangeEntry>();
        return temp;
      }
    }

    private void RemoveIgnorableEvents(IDictionary<FullPath, PathChangeEntry> changes) {
      changes.RemoveWhere(x => !IncludeChange(x.Value));
    }

    private void EnqueueChangeEvent(FullPath rootPath, RelativePath entryPath, PathChangeKind changeKind, PathKind pathKind) {
      //Logger.LogInfo("Enqueue change event: {0}, {1}", path, changeKind);
      var entry = new PathChangeEntry(rootPath, entryPath, changeKind, pathKind);
      GlobalChangeRecorder.RecordChange(new PathChangeRecorder.ChangeInfo {
        Entry = entry,
        TimeStampUtc = _dateTimeProvider.UtcNow,
      });

      lock (_changedPathsLock) {
        MergePathChange(_changedPaths, entry);
      }
    }

    private static void MergePathChange(IDictionary<FullPath, PathChangeEntry> changes, PathChangeEntry entry) {
      var currentChangeKind = PathChangeKind.None;
      var currentPathKind = PathKind.FileOrDirectory;
      PathChangeEntry currentEntry;
      if (changes.TryGetValue(entry.Path, out currentEntry)) {
        currentChangeKind = currentEntry.ChangeKind;
        currentPathKind = currentEntry.PathKind;
      }
      changes[entry.Path] = new PathChangeEntry(
        entry.BasePath,
        entry.RelativePath,
        CombineChangeKinds(currentChangeKind, entry.ChangeKind),
        CombinePathKind(currentPathKind, entry.PathKind));
    }

    private static PathKind CombinePathKind(PathKind current, PathKind next) {
      switch (current) {
        case PathKind.File:
          switch (next) {
            case PathKind.File: return PathKind.File;
            case PathKind.Directory: return PathKind.FileAndDirectory;
            case PathKind.FileOrDirectory: return PathKind.File;
            case PathKind.FileAndDirectory: return PathKind.FileAndDirectory;
            default: throw new ArgumentOutOfRangeException("next");
          }
        case PathKind.Directory:
          switch (next) {
            case PathKind.File: return PathKind.FileAndDirectory;
            case PathKind.Directory: return PathKind.Directory;
            case PathKind.FileOrDirectory: return PathKind.Directory;
            case PathKind.FileAndDirectory: return PathKind.FileAndDirectory;
            default: throw new ArgumentOutOfRangeException("next");
          }
        case PathKind.FileOrDirectory:
          switch (next) {
            case PathKind.File: return PathKind.File;
            case PathKind.Directory: return PathKind.Directory;
            case PathKind.FileOrDirectory: return PathKind.FileOrDirectory;
            case PathKind.FileAndDirectory: return PathKind.FileAndDirectory;
            default: throw new ArgumentOutOfRangeException("next");
          }
        case PathKind.FileAndDirectory:
          switch (next) {
            case PathKind.File: return PathKind.FileAndDirectory;
            case PathKind.Directory: return PathKind.FileAndDirectory;
            case PathKind.FileOrDirectory: return PathKind.FileAndDirectory;
            case PathKind.FileAndDirectory: return PathKind.FileAndDirectory;
            default: throw new ArgumentOutOfRangeException("next");
          }
        default:
          throw new ArgumentOutOfRangeException("current");
      }
    }

    private static PathChangeKind CombineChangeKinds(PathChangeKind current, PathChangeKind next) {
      switch (current) {
        case PathChangeKind.None:
          return next;
        case PathChangeKind.Created:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return current;
            case PathChangeKind.Deleted:
              return PathChangeKind.None;
            case PathChangeKind.Changed:
              return current;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Deleted:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return PathChangeKind.Changed;
            case PathChangeKind.Deleted:
              return current;
            case PathChangeKind.Changed:
              return PathChangeKind.Deleted; // Weird case...
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Changed:
          switch (next) {
            case PathChangeKind.None:
              return current;
            case PathChangeKind.Created:
              return PathChangeKind.Changed; // Weird case...
            case PathChangeKind.Deleted:
              return next;
            case PathChangeKind.Changed:
              return current;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        default:
          throw new ArgumentOutOfRangeException("current");
      }
    }

    /// <summary>
    ///  Skip paths BCL can't process (e.g. path too long)
    /// </summary>
    private bool SkipPath(string path) {
      if (PathHelpers.IsPathTooLong(path)) {
        switch (_logLimiter.Proceed()) {
          case BoundedOperationLimiter.Result.YesAndLast:
            Logger.LogInfo("(The following log message will be the last of its kind)", path);
            goto case BoundedOperationLimiter.Result.Yes;
          case BoundedOperationLimiter.Result.Yes:
            Logger.LogInfo("Skipping file change event because path is too long: \"{0}\"", path);
            break;
          case BoundedOperationLimiter.Result.NoMore:
            break;
        }
        return true;
      }
      if (!PathHelpers.IsValidBclPath(path)) {
        switch (_logLimiter.Proceed()) {
          case BoundedOperationLimiter.Result.YesAndLast:
            Logger.LogInfo("(The following log message will be the last of its kind)", path);
            goto case BoundedOperationLimiter.Result.Yes;
          case BoundedOperationLimiter.Result.Yes:
            Logger.LogInfo("Skipping file change event because path is invalid: \"{0}\"", path);
            break;
          case BoundedOperationLimiter.Result.NoMore:
            break;
        }
        return true;
      }
      return false;
    }
  }
}
