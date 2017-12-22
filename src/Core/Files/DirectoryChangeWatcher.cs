// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
    private readonly PollingDelayPolicy _pathChangesPolling;
    private readonly PollingDelayPolicy _simplePathChangesPolling;
    private readonly PollingDelayPolicy _checkRootsPolling;
    /// <summary>
    /// Dictionary of watchers, one per root directory path.
    /// </summary>
    private readonly IDictionary<FullPath, SingleDirectoryChangeWatcher> _watchers =new Dictionary<FullPath, SingleDirectoryChangeWatcher>();
    private readonly object _watchersLock = new object();

    private HashSet<FullPath> _deletedRootDirectories = new HashSet<FullPath>();
    private readonly object __deletedRootDirectoriesLock = new object();

    /// <summary>
    /// The polling and event posting thread.
    /// </summary>
    private readonly TimeSpan _pollingThreadTimeout = TimeSpan.FromSeconds(1.0);
    private Thread _pollingThread;

    public DirectoryChangeWatcher(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider) {
      _fileSystem = fileSystem;
      _dateTimeProvider = dateTimeProvider;
      _simplePathChangesPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(10.0));
      _pathChangesPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(60.0));
      _checkRootsPolling = new PollingDelayPolicy(dateTimeProvider, TimeSpan.FromSeconds(15.0), TimeSpan.FromSeconds(60.0));
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

    public event EventHandler<PathsChangedEventArgs> PathsChanged;
    public event EventHandler<Exception> Error;

    /// <summary>
    /// Executed on the background thread when changes need to be notified to
    /// our listeners.
    /// </summary>
    protected virtual void OnPathsChanged(PathsChangedEventArgs e) {
      PathsChanged?.Invoke(this, e);
    }

    protected virtual void OnError(Exception e) {
      Error?.Invoke(this, e);
    }

    private void AddDirectory(FullPath directory) {
      SingleDirectoryChangeWatcher watcher;
      lock (_watchersLock) {
        if (_pollingThread == null) {
          _pollingThread = new Thread(ThreadLoop) { IsBackground = true };
          _pollingThread.Start();
        }
        if (_watchers.TryGetValue(directory, out watcher))
          return;

        watcher = new SingleDirectoryChangeWatcher(_fileSystem, _dateTimeProvider, directory);
        _watchers.Add(directory, watcher);
      }

      watcher.Error += WatcherOnError;
      watcher.PathsChanged += WatcherOnPathsChanged;
      watcher.Start();
    }

    private void RemoveDirectory(FullPath directory) {
      SingleDirectoryChangeWatcher watcher;
      lock (_watchersLock) {
        if (!_watchers.TryGetValue(directory, out watcher))
          return;
        _watchers.Remove(directory);
      }
      Logger.LogInfo("Removing directory \"{0}\" from change notification monitoring.", directory);
      watcher.Dispose();
    }

    private void ThreadLoop() {
      Logger.LogInfo("Starting directory change notification monitoring thread.");
      try {
        while (true) {
          _eventReceived.WaitOne(_pollingThreadTimeout);

          CheckDeletedRoots();
          PostPathsChangedEvents();
        }
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in DirectoryChangeWatcher.");
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

      List<KeyValuePair<FullPath, SingleDirectoryChangeWatcher>> deletedWatchers;
      lock (_watchersLock) {
        deletedWatchers = _watchers
          .Where(item => !_fileSystem.DirectoryExists(item.Key))
          .ToList();
        deletedWatchers
          .ForAll(item => {
            RemoveDirectory(item.Key);
          });
      }

      deletedWatchers
        .ForAll(item => {
          EnqueueRootDeletedEvent(item.Key);
        });
    }

    private void PostPathsChangedEvents() {
      Debug.Assert(_pollingThread == Thread.CurrentThread);
      var changedPaths = DequeueChangedPathsEvents();

      // Dequeue events as long as there are new ones showing up as we wait for
      // our polling delays to expire.
      // The goal is to delay generating events as long as there is disk
      // activity within a 10 seconds window. It also allows "merging"
      // consecutive events into more meaningful ones.
      while (changedPaths.Count > 0) {

        // Post changes that belong to an expired polling interval.
        if (_simplePathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(changedPaths, x => x == PathChangeKind.Changed);
          _simplePathChangesPolling.Restart();
        }
        if (_pathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(changedPaths, x => true);
          _pathChangesPolling.Restart();
        }

        // If we are done, exit to waiting thread
        if (changedPaths.Count == 0)
          break;

        // If there are leftover paths, this means some polling interval(s) have
        // not expired. Go back to sleeping for a little bit or until we receive
        // a new change event.
        _eventReceived.WaitOne(_pollingThreadTimeout);

        // See if we got new events, and merge them.
        var morePathsChanged = DequeueChangedPathsEvents();
        morePathsChanged.ForAll(item => MergePathChange(changedPaths, item.Value));

        // If we got more changes, reset the polling interval for the non-simple
        // path changed. The goal is to avoid processing those too frequently if
        // there is activity on disk (e.g. a build happening), because
        // processing add/delete changes is currently much more expensive in the
        // search engine file database.
        // Note we don't update the simple "file change" events, as those as cheaper
        // to process.
        if (morePathsChanged.Count > 0) {
          _simplePathChangesPolling.Checkpoint();
          _pathChangesPolling.Checkpoint();
        }
      }

      // We are done processing all changes, make sure we wait at least some
      // amount of time before processing anything more.
      _simplePathChangesPolling.Restart();
      _pathChangesPolling.Restart();

      Debug.Assert(changedPaths.Count == 0);
    }

    private static void MergePathChange(IDictionary<FullPath, PathChangeEntry> changes, PathChangeEntry entry) {
      var entryPath = entry.Path;
      PathChangeKind previousChangeKind = PathChangeKind.None;
      PathChangeEntry currentEntry;
      if (changes.TryGetValue(entryPath, out currentEntry)) {
        previousChangeKind = currentEntry.Kind;
      }

      // Merge change kinds
      var newChangeKind = SingleDirectoryChangeWatcher.CombineChangeKinds(previousChangeKind, entry.Kind);

      // Update table with new change kind
      if (newChangeKind == PathChangeKind.None) {
        // Remove entry for files that have been created then deleted
        changes.Remove(entryPath);
      } else {
        // Update entry for other cases
        changes[entryPath] = new PathChangeEntry(entry.BasePath, entry.RelativePath, newChangeKind);
      }
    }

    /// <summary>
    /// Filter, remove and post events for all changes in <paramref
    /// name="paths"/> that match <paramref name="predicate"/>
    /// </summary>
    private void PostPathsChangedEvents(IDictionary<FullPath, PathChangeEntry> paths, Func<PathChangeKind, bool> predicate) {
      RemoveIgnorableEvents(paths);

      var changes = paths
        .Where(x => predicate(x.Value.Kind))
        .Select(x => x.Value)
        .ToList();
      if (changes.Count == 0)
        return;

      changes.ForAll(x => paths.Remove(x.Path));
      if (changes.Count > 0) {
        OnPathsChanged(new PathsChangedEventArgs { Changes = changes });
      }
    }

    private bool IncludeChange(PathChangeEntry entry) {
      // Ignore changes for files that have been created then deleted
      if (entry.Kind == PathChangeKind.None)
        return false;


      return true;
    }

    private IDictionary<FullPath, PathChangeEntry> DequeueChangedPathsEvents() {
      // Copy current changes into temp and reset to empty collection.
      Dictionary<FullPath, PathChangeEntry> result = new Dictionary<FullPath, PathChangeEntry>();
      lock (__deletedRootDirectoriesLock) {
        foreach (var path in _deletedRootDirectories) {
          result.Add(path, new PathChangeEntry(path, RelativePath.Empty, PathChangeKind.Deleted));
        }
        _deletedRootDirectories.Clear();
      }
      lock (_watchersLock) {
        foreach (var dir in _watchers.Values) {
          foreach (var change in dir.DequeueChangedPathsEvents()) {
            var path = dir.DirectoryPath.Combine(change.Key);
            result.Add(path, new PathChangeEntry(dir.DirectoryPath, change.Key, change.Value));
          }
        }
      }
      return result;
    }

    private void RemoveIgnorableEvents(IDictionary<FullPath, PathChangeEntry> changes) {
      changes.RemoveWhere(x => !IncludeChange(x.Value));
    }

    private void EnqueueRootDeletedEvent(FullPath rootPath) {
      lock (__deletedRootDirectoriesLock) {
        _deletedRootDirectories.Add(rootPath);
      }
    }

    private void WatcherOnError(object sender, Exception exception) {
      Logger.WrapActionInvocation(() => {
        Logger.LogError(exception, "File system watcher for path \"{0}\" error.",
          ((SingleDirectoryChangeWatcher)sender).DirectoryPath);
        OnError(exception);
      });
    }

    private void WatcherOnPathsChanged(object sender, EventArgs e) {
      _eventReceived.Set();
    }

    private class PollingDelayPolicy {
      private readonly IDateTimeProvider _dateTimeProvider;
      private readonly TimeSpan _checkpointDelay;
      private readonly TimeSpan _maxDelay;
      private DateTime _lastPollUtc;
      private DateTime _lastCheckpointUtc;

      private static class ClassLogger {
        static ClassLogger() {
#if DEBUG
          //LogInfoEnabled = true;
#endif
        }
        // ReSharper disable once UnusedAutoPropertyAccessor.Local
        public static bool LogInfoEnabled { get; set; }
      }

      public PollingDelayPolicy(IDateTimeProvider dateTimeProvider, TimeSpan checkpointDelay, TimeSpan maxDelay) {
        _dateTimeProvider = dateTimeProvider;
        _checkpointDelay = checkpointDelay;
        _maxDelay = maxDelay;
        Restart();
      }

      /// <summary>
      /// Called when all events have been flushed, resets all timers.
      /// </summary>
      public void Restart() {
        _lastPollUtc = _lastCheckpointUtc = _dateTimeProvider.UtcNow;
      }

      /// <summary>
      /// Called when a new event instance occurred, resets the "checkpoint"
      /// timer.
      /// </summary>
      public void Checkpoint() {
        _lastCheckpointUtc = _dateTimeProvider.UtcNow;
      }

      /// <summary>
      /// Returns <code>true</code> when either the maxmium or checkpoint delay
      /// has expired.
      /// </summary>
      public bool WaitTimeExpired() {
        var now = _dateTimeProvider.UtcNow;

        var result = (now - _lastPollUtc >= _maxDelay) ||
                     (now - _lastCheckpointUtc >= _checkpointDelay);
        if (result) {
          if (ClassLogger.LogInfoEnabled) {
            Logger.LogInfo("Timer expired: now={0}, checkpoint={1} msec, start={2} msec, checkpointDelay={3:n0} msec, maxDelay={4:n0} msec",
              now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
              (now - _lastCheckpointUtc).TotalMilliseconds,
              (now - _lastPollUtc).TotalMilliseconds,
              _checkpointDelay.TotalMilliseconds,
              _maxDelay.TotalMilliseconds);
          }
        }
        return result;
      }
    }
  }
}
