// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  public class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    private readonly IFileSystem _fileSystem;
    private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
    private readonly TimeSpan _eventReceivedTimeout = TimeSpan.FromSeconds(10.0);
    private readonly TimeSpan _pathsChangedDelay = TimeSpan.FromSeconds(10.0);
    private readonly TimeSpan _simplePathsChangedDelay = TimeSpan.FromSeconds(1.0);

    private readonly Dictionary<FullPath, FileSystemWatcher> _watchers = new Dictionary<FullPath, FileSystemWatcher>();
    private readonly object _watchersLock = new object();

    private Dictionary<FullPath, PathChangeKind> _changedPaths = new Dictionary<FullPath, PathChangeKind>();
    private readonly object _changedPathsLock = new object();
    private Thread _thread;

    public DirectoryChangeWatcher(IFileSystem fileSystem) {
      _fileSystem = fileSystem;
    }

    public void WatchDirectories(IEnumerable<FullPath> directories) {
      lock (_watchersLock) {
        var oldSet = new HashSet<FullPath>(_watchers.Keys);
        var newSet = new HashSet<FullPath>(directories);

        var removed = new HashSet<FullPath>(oldSet);
        removed.ExceptWith(newSet);

        var added = new HashSet<FullPath>(newSet);
        added.ExceptWith(oldSet);

        removed.ForAll(x => RemoveDirectory(x));
        added.ForAll(x => AddDirectory(x));
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;

    private void AddDirectory(FullPath directory) {
      FileSystemWatcher watcher;
      lock (_watchersLock) {
        if (_thread == null) {
          _thread = new Thread(ThreadLoop) {IsBackground = true};
          _thread.Start();
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
      while (true) {
        _eventReceived.WaitOne(_eventReceivedTimeout);

        CheckDeletedRoots();
        PostPathsChangedEvents();
      }
    }

    /// <summary>
    /// The OS FileSystem notification does not notify us if a directory used for
    /// change notification is deleted (or renamed). We have to use polling to detect
    /// this kind of changes.
    /// </summary>
    private void CheckDeletedRoots() {
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
      var paths = DequeueEvents();
      if (paths.Count == 0)
        return;

      // Dequeue events as long as there are new ones within a 10 seconds delay.
      // The goal is to delay generating events as long as there is disk
      // activity within a 10 seconds window. It also allows "merging"
      // consecutive events into more meaningful ones.
      while (true) {
        Thread.Sleep(_simplePathsChangedDelay);
        var morePathsChanged = DequeueEvents();
        // Merge changes
        morePathsChanged.ForAll(change => MergePathChange(paths, change.Key, change.Value));
        PostSimplePathsChangedEvents(paths);

        Thread.Sleep(_pathsChangedDelay);
        morePathsChanged = DequeueEvents();
        if (morePathsChanged.Count == 0)
          break;

        // Merge changes
        morePathsChanged.ForAll(change => MergePathChange(paths, change.Key, change.Value));
      }

      //
      var entries = paths
        .Select(x => new PathChangeEntry(x.Key, x.Value))
        .Where(item => IncludeChange(item))
        .ToList();
      OnPathsChanged(entries);
    }

    private void PostSimplePathsChangedEvents(Dictionary<FullPath, PathChangeKind> paths) {
      var simpleChanges = paths
        .Where(x => x.Value == PathChangeKind.Changed)
        .Select(x => new PathChangeEntry(x.Key, x.Value))
        .Where(item => IncludeChange(item))
        .ToList();
      if (simpleChanges.Count == 0)
        return;

      simpleChanges.ForAll(x => paths.Remove(x.Path));
      OnPathsChanged(simpleChanges);
    }

    private bool IncludeChange(PathChangeEntry change) {
      var path = change.Path;
      var changeType = change.Kind;

      // Ignore changes for files that have been created then deleted
      if (changeType == PathChangeKind.None)
        return false;

      // Creation/changes to a directory entry are irrelevant (we will get notifications
      // for files inside the directory is anything relevant occured.)
      if (_fileSystem.DirectoryExists(path)) {
        if (changeType == PathChangeKind.Changed || changeType == PathChangeKind.Created)
          return false;
      }

      return true;
    }

    private Dictionary<FullPath, PathChangeKind> DequeueEvents() {
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<FullPath, PathChangeKind>();
        return temp;
      }
    }

    private void WatcherOnError(object sender, ErrorEventArgs errorEventArgs) {
      // TODO(rpaquay): Try to recover?
      Logger.LogException(errorEventArgs.GetException(), "File system watcher for path \"{0}\" error.",
                          ((FileSystemWatcher)sender).Path);
    }

    private void EnqueueChangeEvent(FullPath path, PathChangeKind changeKind) {
      lock (_changedPathsLock) {
        MergePathChange(_changedPaths, path, changeKind);
      }
    }

    private static void MergePathChange(Dictionary<FullPath, PathChangeKind> changes, FullPath path, PathChangeKind kind) {
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
        Logger.Log("Skipping changed event because path is too long: \"{0}\"", path);
        return true;
      }
      if (!PathHelpers.IsValidBclPath(path)) {
        Logger.Log("Skipping changed event because path is invalid: \"{0}\"", path);
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

      Logger.Log("OnPathsChanged: {0} items.", changes.Count);
      // Too verbose
      //changes.ForAll(change => Logger.Log("OnPathsChanged({0}).", change));
      var handler = PathsChanged;
      if (handler != null)
        handler(changes);
    }
  }
}
