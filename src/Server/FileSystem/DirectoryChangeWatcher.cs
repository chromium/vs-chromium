// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core;
using VsChromium.Core.FileNames;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem {
  public class DirectoryChangeWatcher : IDirectoryChangeWatcher {
    private readonly object _changedPathsLock = new object();
    private readonly AutoResetEvent _eventReceived = new AutoResetEvent(false);
    private readonly TimeSpan _eventReceivedTimeout = TimeSpan.FromSeconds(10.0);

    private readonly Dictionary<DirectoryName, FileSystemWatcher> _watchers =
      new Dictionary<DirectoryName, FileSystemWatcher>();

    private readonly object _watchersLock = new object();
    private Dictionary<string, PathChangeKind> _changedPaths = new Dictionary<string, PathChangeKind>(SystemPathComparer.Instance.Comparer);
    private Thread _thread;
    private readonly TimeSpan _pathChangedDelay = TimeSpan.FromSeconds(10.0);

    public void WatchDirectories(IEnumerable<DirectoryName> directories) {
      lock (_watchersLock) {
        var oldSet = new HashSet<DirectoryName>(_watchers.Keys);
        var newSet = new HashSet<DirectoryName>(directories);

        var removed = new HashSet<DirectoryName>(oldSet);
        removed.ExceptWith(newSet);

        var added = new HashSet<DirectoryName>(newSet);
        added.ExceptWith(oldSet);

        removed.ForAll(x => RemoveDirectory(x));
        added.ForAll(x => AddDirectory(x));
      }
    }

    public event Action<IList<PathChangeEntry>> PathsChanged;

    private void AddDirectory(DirectoryName directory) {
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

      Logger.Log("Starting monitoring directory \"{0}\" for change notifications.", directory.GetFullName());
      watcher.Path = directory.GetFullName();
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

    private void RemoveDirectory(DirectoryName directory) {
      FileSystemWatcher watcher;
      lock (_watchersLock) {
        if (!_watchers.TryGetValue(directory, out watcher))
          return;
        _watchers.Remove(directory);
      }
      Logger.Log("Removing directory \"{0}\" from change notification monitoring.", directory.GetFullName());
      watcher.Dispose();
    }

    private void ThreadLoop() {
      Logger.Log("Starting directory change notification monitoring thread.");
      while (true) {
        _eventReceived.WaitOne(_eventReceivedTimeout);

        CheckDeletedRoots();

        var pathsChanged = BatchPathChangedEvents();
        if (pathsChanged.Count > 0)
          OnPathsChanged(pathsChanged);
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
          .Where(item => !Directory.Exists(item.Key.GetFullName()))
          .ToList();

        deletedWatchers
          .ForAll(item => {
            EnqueueChangeEvent(item.Key.GetFullName(), PathChangeKind.Deleted);
            RemoveDirectory(item.Key);
          });
      }
    }

    private IList<PathChangeEntry> BatchPathChangedEvents() {
      var paths = DequeueEvents();
      if (paths.Count == 0)
        return new List<PathChangeEntry>();

      // Dequeue events as long as there are new ones within a 10 seconds delay.
      // The goal is to delay generating events as long as there is disk
      // activity within a 10 seconds window. It also allows "merging"
      // consecutive events into more meaningful ones.
      while (true) {
        Thread.Sleep(_pathChangedDelay);
        var morePathsChanged = DequeueEvents();
        if (morePathsChanged.Count == 0)
          break;

        // Merge changes
        morePathsChanged.ForAll(change => AddFileChange(paths, change.Key, change.Value));
      }

      //
      return paths
        .Select(x => new PathChangeEntry(x.Key, x.Value))
        .Where(item => IncludeChange(item))
        .ToList();
    }

    private bool IncludeChange(PathChangeEntry change) {
      var path = change.Path;
      var changeType = change.Kind;

      // Ignore changes for files that have been created then deleted
      if (changeType == PathChangeKind.None)
        return false;

      // Creation/changes to a directory entry are irrelevant (we will get notifications
      // for files inside the directory is anything relevant occured.)
      if (Directory.Exists(path)) {
        if (changeType == PathChangeKind.Changed || changeType == PathChangeKind.Created)
          return false;
      }

      return true;
    }

    private Dictionary<string, PathChangeKind> DequeueEvents() {
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<string, PathChangeKind>(SystemPathComparer.Instance.Comparer);
        return temp;
      }
    }

    private void WatcherOnError(object sender, ErrorEventArgs errorEventArgs) {
      // TODO(rpaquay): Try to recover?
      Logger.LogException(errorEventArgs.GetException(), "File system watcher for path \"{0}\" error.",
                          ((FileSystemWatcher)sender).Path);
    }

    private void EnqueueChangeEvent(string path, PathChangeKind changeKind) {
      lock (_changedPathsLock) {
        AddFileChange(_changedPaths, path, changeKind);
      }
    }

    private static void AddFileChange(Dictionary<string, PathChangeKind> dic, string path, PathChangeKind newChangeKind) {
      PathChangeKind currentChangeKind;
      if (!dic.TryGetValue(path, out currentChangeKind)) {
        currentChangeKind = PathChangeKind.None;
      }
      dic[path] = CombineChangeTypes(currentChangeKind, newChangeKind);
    }

    private static PathChangeKind CombineChangeTypes(PathChangeKind initial, PathChangeKind next) {
      switch (initial) {
        case PathChangeKind.None:
          return next;
        case PathChangeKind.Created:
          switch (next) {
            case PathChangeKind.None:
              return initial;
            case PathChangeKind.Created:
              return initial;
            case PathChangeKind.Deleted:
              return PathChangeKind.None;
            case PathChangeKind.Changed:
              return initial;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Deleted:
          switch (next) {
            case PathChangeKind.None:
              return initial;
            case PathChangeKind.Created:
              return PathChangeKind.Changed;
            case PathChangeKind.Deleted:
              return initial;
            case PathChangeKind.Changed:
              return PathChangeKind.Deleted; // Weird case...
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case PathChangeKind.Changed:
          switch (next) {
            case PathChangeKind.None:
              return initial;
            case PathChangeKind.Created:
              return PathChangeKind.Changed; // Weird case...
            case PathChangeKind.Deleted:
              return next;
            case PathChangeKind.Changed:
              return initial;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        default:
          throw new ArgumentOutOfRangeException("initial");
      }
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs renamedEventArgs) {
      EnqueueChangeEvent(renamedEventArgs.OldFullPath, PathChangeKind.Deleted);
      EnqueueChangeEvent(renamedEventArgs.FullPath, PathChangeKind.Created);
      _eventReceived.Set();
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, PathChangeKind.Deleted);
      _eventReceived.Set();
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, PathChangeKind.Created);
      _eventReceived.Set();
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, PathChangeKind.Changed);
      _eventReceived.Set();
    }

    protected virtual void OnPathsChanged(IList<PathChangeEntry> changes) {
      Logger.Log("OnPathsChanged: {0} items.", changes.Count);
      // Too verbose
      //changes.ForAll(change => Logger.Log("OnPathsChanged({0}).", change));
      var handler = PathsChanged;
      if (handler != null)
        handler(changes);
    }
  }
}
