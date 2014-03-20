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

    private readonly Dictionary<DirectoryName, FileSystemWatcher> _watchers =
      new Dictionary<DirectoryName, FileSystemWatcher>();

    private readonly object _watchersLock = new object();
    private Dictionary<string, ChangeType> _changedPaths = new Dictionary<string, ChangeType>(SystemPathComparer.Instance.Comparer);
    private Thread _thread;

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

    public event Action<IList<KeyValuePair<string, ChangeType>>> PathsChanged;

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
        _eventReceived.WaitOne(TimeSpan.FromSeconds(10.0));

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
            EnqueueChangeEvent(item.Key.GetFullName(), ChangeType.Deleted);
            RemoveDirectory(item.Key);
          });
      }
    }

    private List<KeyValuePair<string, ChangeType>> BatchPathChangedEvents() {
      var paths = DequeueEvents();
      if (paths.Count == 0)
        return paths.ToList();

      while (true) {
        // Delay events by 10 sec to 1) batch changes and 2) avoid race conditions as much as possible.
        Thread.Sleep(TimeSpan.FromSeconds(10.0));
        var morePathsChanged = DequeueEvents();
        if (morePathsChanged.Count == 0)
          break;

        // Merge changes
        morePathsChanged.ForAll(change => AddFileChange(paths, change.Key, change.Value));
      }

      //
      return paths
        .Where(item => IncludeChange(item))
        .ToList();
    }

    private bool IncludeChange(KeyValuePair<string, ChangeType> fileChange) {
      var path = fileChange.Key;
      var changeType = fileChange.Value;

      // Ignore changes for files that have been created then deleted
      if (changeType == ChangeType.None)
        return false;

      // Creation/changes to a directory entry are irrelevant (we will get notifications
      // for files inside the directory is anything relevant occured.)
      if (Directory.Exists(path)) {
        if (changeType == ChangeType.Changed || changeType == ChangeType.Created)
          return false;
      }

      return true;
    }

    private Dictionary<string, ChangeType> DequeueEvents() {
      lock (_changedPathsLock) {
        var temp = _changedPaths;
        _changedPaths = new Dictionary<string, ChangeType>(SystemPathComparer.Instance.Comparer);
        return temp;
      }
    }

    private void WatcherOnError(object sender, ErrorEventArgs errorEventArgs) {
      // TODO(rpaquay): Try to recover?
      Logger.LogException(errorEventArgs.GetException(), "File system watcher for path \"{0}\" error.",
                          ((FileSystemWatcher)sender).Path);
    }

    private void EnqueueChangeEvent(string path, ChangeType changeType) {
      lock (_changedPathsLock) {
        AddFileChange(_changedPaths, path, changeType);
      }
    }

    private static void AddFileChange(Dictionary<string, ChangeType> dic, string path, ChangeType newChangeType) {
      ChangeType currentChangeType;
      if (!dic.TryGetValue(path, out currentChangeType)) {
        currentChangeType = ChangeType.None;
      }
      dic[path] = CombineChangeTypes(currentChangeType, newChangeType);
    }

    private static ChangeType CombineChangeTypes(ChangeType initial, ChangeType next) {
      switch (initial) {
        case ChangeType.None:
          return next;
        case ChangeType.Created:
          switch (next) {
            case ChangeType.None:
              return initial;
            case ChangeType.Created:
              return initial;
            case ChangeType.Deleted:
              return ChangeType.None;
            case ChangeType.Changed:
              return initial;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case ChangeType.Deleted:
          switch (next) {
            case ChangeType.None:
              return initial;
            case ChangeType.Created:
              return ChangeType.Changed;
            case ChangeType.Deleted:
              return initial;
            case ChangeType.Changed:
              return ChangeType.Deleted; // Weird case...
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        case ChangeType.Changed:
          switch (next) {
            case ChangeType.None:
              return initial;
            case ChangeType.Created:
              return ChangeType.Changed; // Weird case...
            case ChangeType.Deleted:
              return next;
            case ChangeType.Changed:
              return initial;
            default:
              throw new ArgumentOutOfRangeException("next");
          }
        default:
          throw new ArgumentOutOfRangeException("initial");
      }
    }

    private void WatcherOnRenamed(object sender, RenamedEventArgs renamedEventArgs) {
      EnqueueChangeEvent(renamedEventArgs.OldFullPath, ChangeType.Deleted);
      EnqueueChangeEvent(renamedEventArgs.FullPath, ChangeType.Created);
      _eventReceived.Set();
    }

    private void WatcherOnDeleted(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, ChangeType.Deleted);
      _eventReceived.Set();
    }

    private void WatcherOnCreated(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, ChangeType.Created);
      _eventReceived.Set();
    }

    private void WatcherOnChanged(object sender, FileSystemEventArgs fileSystemEventArgs) {
      EnqueueChangeEvent(fileSystemEventArgs.FullPath, ChangeType.Changed);
      _eventReceived.Set();
    }

    protected virtual void OnPathsChanged(IList<KeyValuePair<string, ChangeType>> changes) {
      Logger.Log("OnPathsChanged: {0} items.", changes.Count);
      // Too verbose
      //changes.ForAll(change => Logger.Log("OnPathsChanged({0}).", change));
      var handler = PathsChanged;
      if (handler != null)
        handler(changes);
    }
  }
}
