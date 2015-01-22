// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    private readonly IFileSystem _fileSystem;
    private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
    private readonly PollingHelper _pathChangesPolling;
    private readonly PollingHelper _simplePathChangesPolling;
    private readonly PollingHelper _checkRootsPolling;

    /// <summary>
    /// Dictionary of watchers, one per root directory path.
    /// </summary>
    private readonly Dictionary<FullPath, FileSystemWatcher> _watchers = new Dictionary<FullPath, FileSystemWatcher>();
    private readonly object _watchersLock = new object();

    /// <summary>
    /// Dictionary of file change events, per path.
    /// </summary>
    private Dictionary<FullPath, PathChangeKind> _changedPaths = new Dictionary<FullPath, PathChangeKind>();
    private readonly object _changedPathsLock = new object();

    /// <summary>
    /// The polling and event posting thread.
    /// </summary>
    private readonly TimeSpan _pollingThreadTimeout = TimeSpan.FromSeconds(1.0);
    private Thread _pollingThread;

    public DirectoryChangeWatcher(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider) {
      _fileSystem = fileSystem;
      _simplePathChangesPolling = new PollingHelper(dateTimeProvider, TimeSpan.FromSeconds(1.0));
      _pathChangesPolling = new PollingHelper(dateTimeProvider, TimeSpan.FromSeconds(10.0));
      _checkRootsPolling = new PollingHelper(dateTimeProvider, TimeSpan.FromSeconds(15.0));
    }

    public void WatchDirectories(IEnumerable<FullPath> directories) {
      lock (_watchersLock) {
        var oldSet = new HashSet<FullPath>(_watchers.Keys);
        var newSet = new HashSet<FullPath>(directories);

        var removed = new HashSet<FullPath>(oldSet);
        removed.ExceptWith(newSet);

        var added = new HashSet<FullPath>(newSet);
        added.ExceptWith(oldSet);

        removed.ForAll(RemoveDirectory);
        added.ForAll(AddDirectory);
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;

    private void AddDirectory(FullPath directory) {
      FileSystemWatcher watcher;
      lock (_watchersLock) {
        if (_pollingThread == null) {
          _pollingThread = new Thread(ThreadLoop) { IsBackground = true };
          _pollingThread.Start();
        }
        if (_watchers.TryGetValue(directory, out watcher))
          return;

        watcher = new FileSystemWatcher();
        _watchers.Add(directory, watcher);
      }

      Logger.Log("Starting monitoring directory \"{0}\" for change notifications.", directory);
      watcher.Path = directory.Value;
      watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.DirectoryName | NotifyFilters.FileName;
      watcher.IncludeSubdirectories = true;
      watcher.InternalBufferSize = 50 * 1024; // 50KB sounds more reasonable than 8KB
      watcher.Changed += WatcherOnChanged;
      watcher.Created += WatcherOnCreated;
      watcher.Deleted += WatcherOnDeleted;
      watcher.Renamed += WatcherOnRenamed;
      watcher.Error += WatcherOnError;
      watcher.EnableRaisingEvents = true;
    }

    private void RemoveDirectory(FullPath directory) {
      FileSystemWatcher watcher;
      lock (_watchersLock) {
        if (!_watchers.TryGetValue(directory, out watcher))
          return;
        _watchers.Remove(directory);
      }
      Logger.Log("Removing directory \"{0}\" from change notification monitoring.", directory);
      watcher.Dispose();
    }

    private void ThreadLoop() {
      Logger.Log("Starting directory change notification monitoring thread.");
      try {
        while (true) {
          _eventReceived.WaitOne(_pollingThreadTimeout);

          CheckDeletedRoots();
          PostPathsChangedEvents();
        }
      }
      catch (Exception e) {
        Logger.LogException(e, "Error in DirectoryChangeWatcher.");
      }
    }

    /// <summary>
    /// The OS FileSystem notification does not notify us if a directory used for
    /// change notification is deleted (or renamed). We have to use polling to detect
    /// this kind of changes.
    /// </summary>
    private void CheckDeletedRoots() {
      Debug.Assert(_pollingThread == Thread.CurrentThread);
      if (!_checkRootsPolling.WaitTimeExpired())
        return;
      _checkRootsPolling.Restart();

      lock (_watchersLock) {
        var deletedWatchers = _watchers
          .Where(item => !_fileSystem.DirectoryExists(item.Key))
          .ToList();

        deletedWatchers
          .ForAll(item => {
            EnqueueChangeEvent(item.Key, PathChangeKind.Deleted);
            RemoveDirectory(item.Key);
          });
      }
    }

    private void PostPathsChangedEvents() {
      Debug.Assert(_pollingThread == Thread.CurrentThread);
      var paths = DequeueEvents();

      // Dequeue events as long as there are new ones showing up as we wait for
      // our polling delays to expire.
      // The goal is to delay generating events as long as there is disk
      // activity within a 10 seconds window. It also allows "merging"
      // consecutive events into more meaningful ones.
      while (paths.Count > 0) {

        // Post changes that belong to an expired polling interval.
        if (_simplePathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(paths, x => x == PathChangeKind.Changed);
        }
        if (_pathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(paths, x => true);
        }

        // If we are done, exit to waiting thread
        if (paths.Count == 0)
          break;

        // If there are leftover paths, this means some polling interval(s) have
        // not expired. Go back to sleeping for a little bit or until we receive
        // a new change event.
        _eventReceived.WaitOne(_pollingThreadTimeout);

        // See if we got new events, and merge them.
        var morePathsChanged = DequeueEvents();
        morePathsChanged.ForAll(change => MergePathChange(paths, change.Key, change.Value));

        // If we got more changes, reset the polling interval for the non-simple
        // path changed. The goal is to avoid processing those too frequently if
        // there is activity on disk (e.g. a build happening), because
        // processing add/delete changes is currently much more expensive in the
        // search engine file database.
        // Note we don't update the simple "file change" events, as those as cheaper
        // to process.
        if (morePathsChanged.Count > 0) {
          _pathChangesPolling.Restart();
        }
      }

      // We are done processing all changes, make sure we wait at least some
      // amount of time before processing anything more.
      _simplePathChangesPolling.Restart();
      _pathChangesPolling.Restart();

      Debug.Assert(paths.Count == 0);
    }

    /// <summary>
    /// Filter, remove and post events for all changes in <paramref
    /// name="paths"/> that match <paramref name="predicate"/>
    /// </summary>
    private void PostPathsChangedEvents(IDictionary<FullPath, PathChangeKind> paths, Func<PathChangeKind, bool> predicate) {
      FilterEvents(paths);

      var changes = paths
        .Where(x => predicate(x.Value))
        .Select(x => new PathChangeEntry(x.Key, x.Value))
        .ToList();
      if (changes.Count == 0)
        return;

      changes.ForAll(x => paths.Remove(x.Path));
      OnPathsChanged(changes);
    }

    private bool IncludeChange(FullPath path, PathChangeKind changeKind) {
      // Ignore changes for files that have been created then deleted
      if (changeKind == PathChangeKind.None)
        return false;

      // Creation/changes to a directory entry are irrelevant (we will get notifications
      // for files inside the directory is anything relevant occured.)
      if (_fileSystem.DirectoryExists(path)) {
        if (changeKind == PathChangeKind.Changed || changeKind == PathChangeKind.Created)
          return false;
      }

      return true;
    }

    private IDictionary<FullPath, PathChangeKind> DequeueEvents() {
      // Copy current changes into temp and reset to empty collection.
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<FullPath, PathChangeKind>();
        return temp;
      }
    }

    private void FilterEvents(IDictionary<FullPath, PathChangeKind> temp) {
      temp.RemoveWhere(x => !IncludeChange(x.Key, x.Value));
    }

    private void WatcherOnError(object sender, ErrorEventArgs errorEventArgs) {
      // TODO(rpaquay): Try to recover?
      Logger.LogException(errorEventArgs.GetException(), "File system watcher for path \"{0}\" error.",
                          ((FileSystemWatcher)sender).Path);
    }

    private void EnqueueChangeEvent(FullPath path, PathChangeKind changeKind) {
      //Logger.Log("Enqueue change event: {0}, {1}", path, changeKind);
      lock (_changedPathsLock) {
        MergePathChange(_changedPaths, path, changeKind);
      }
    }

    private static void MergePathChange(IDictionary<FullPath, PathChangeKind> changes, FullPath path, PathChangeKind kind) {
      PathChangeKind currentChangeKind;
      if (!changes.TryGetValue(path, out currentChangeKind)) {
        currentChangeKind = PathChangeKind.None;
      }
      changes[path] = CombineChangeKinds(currentChangeKind, kind);
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
        Logger.Log("Skipping file change event because path is too long: \"{0}\"", path);
        return true;
      }
      if (!PathHelpers.IsValidBclPath(path)) {
        Logger.Log("Skipping file change event because path is invalid: \"{0}\"", path);
        return true;
      }
      return false;
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs renamedEventArgs) {
      if (SkipPath(renamedEventArgs.OldFullPath))
        return;
      if (SkipPath(renamedEventArgs.FullPath))
        return;

      EnqueueChangeEvent(new FullPath(renamedEventArgs.OldFullPath), PathChangeKind.Deleted);
      EnqueueChangeEvent(new FullPath(renamedEventArgs.FullPath), PathChangeKind.Created);
      _eventReceived.Set();
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) {
      if (SkipPath(fileSystemEventArgs.FullPath))
        return;

      EnqueueChangeEvent(new FullPath(fileSystemEventArgs.FullPath), PathChangeKind.Deleted);
      _eventReceived.Set();
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) {
      if (SkipPath(fileSystemEventArgs.FullPath))
        return;

      EnqueueChangeEvent(new FullPath(fileSystemEventArgs.FullPath), PathChangeKind.Created);
      _eventReceived.Set();
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) {
      if (SkipPath(fileSystemEventArgs.FullPath))
        return;

      EnqueueChangeEvent(new FullPath(fileSystemEventArgs.FullPath), PathChangeKind.Changed);
      _eventReceived.Set();
    }

    /// <summary>
    /// Executed on the background thread when changes need to be notified to
    /// our listeners.
    /// </summary>
    protected virtual void OnPathsChanged(IList<PathChangeEntry> changes) {
      if (changes.Count == 0)
        return;

      //Logger.Log("DirectoryChangedWatcher.OnPathsChanged: {0} items (logging max 5 below).", changes.Count);
      //changes.Take(5).ForAll(x => 
      //  Logger.Log("  Path changed: \"{0}\", {1}.", x.Path, x.Kind));
      var handler = PathsChanged;
      if (handler != null)
        handler(changes);
    }

    private class PollingHelper {
      private readonly IDateTimeProvider _dateTimeProvider;
      private readonly TimeSpan _delay;
      private DateTime _lastPollUtc;
      public PollingHelper(IDateTimeProvider dateTimeProvider, TimeSpan delay) {
        _dateTimeProvider = dateTimeProvider;
        // Initialize as if the last time we posted events is "now".
        _lastPollUtc = dateTimeProvider.UtcNow;
        _delay = delay;
      }

      public void Restart() {
        _lastPollUtc = _dateTimeProvider.UtcNow;
      }

      public bool WaitTimeExpired() {
        return _lastPollUtc + _delay < _dateTimeProvider.UtcNow;
      }
    }
  }
}
