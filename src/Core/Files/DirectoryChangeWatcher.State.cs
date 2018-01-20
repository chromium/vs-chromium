// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// The common base class of all possible states
    /// </summary>
    private abstract class State {
      private readonly StateHost _stateHost;

      protected State(StateHost stateHost) {
        Logger.LogInfo("DirectoryWatcher: Entering {0} state", GetType().Name);
        _stateHost = stateHost;
      }

      public StateHost StateHost {
        get { return _stateHost; }
      }

      public IDateTimeProvider DateTimeProvider {
        get { return StateHost.ParentWatcher._dateTimeProvider; }
      }

      public virtual void OnStateActive() { }
      public abstract State OnPause();
      public abstract State OnResume();
      public abstract State OnWatchDirectories(IEnumerable<FullPath> directories);
      public abstract State OnPolling();
      public abstract State OnWatcherErrorEvent(object sender, ErrorEventArgs args);
      public abstract State OnWatcherFileChangedEvent(object sender, FileSystemEventArgs args, PathKind pathKind);
      public abstract State OnWatcherFileCreatedEvent(object sender, FileSystemEventArgs args, PathKind pathKind);
      public abstract State OnWatcherFileDeletedEvent(object sender, FileSystemEventArgs args, PathKind pathKind);
      public abstract State OnWatcherFileRenamedEvent(object sender, RenamedEventArgs args, PathKind pathKind);
      public abstract State OnWatcherAdded(FullPath directory, DirectoryWatcherhEntry watcher);
      public abstract State OnWatcherRemoved(FullPath directory, DirectoryWatcherhEntry watcher);

      public virtual State OnDispose() {
        return new DisposedState(StateHost);
      }

      protected void WatchDirectoriesImpl(IEnumerable<FullPath> directories) {
        lock (StateHost.ParentWatcher._watchersLock) {
          var oldSet = new HashSet<FullPath>(StateHost.ParentWatcher._watchers.Keys);
          var newSet = new HashSet<FullPath>(directories);

          var removed = new HashSet<FullPath>(oldSet);
          removed.ExceptWith(newSet);

          var added = new HashSet<FullPath>(newSet);
          added.ExceptWith(oldSet);

          removed.ForAll(RemoveDirectory);
          added.ForAll(AddDirectory);
        }
      }

      protected void AddDirectory(FullPath directory) {
        DirectoryWatcherhEntry watcherEntry;
        lock (StateHost.ParentWatcher._watchersLock) {
          StateHost.PollingThread.Start();
          if (StateHost.ParentWatcher._watchers.TryGetValue(directory, out watcherEntry))
            return;

          watcherEntry = new DirectoryWatcherhEntry {
            Path = directory,
            DirectoryNameWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
            FileNameWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
            FileWriteWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
          };
          StateHost.ParentWatcher._watchers.Add(directory, watcherEntry);
        }

        Logger.LogInfo("Starting monitoring directory \"{0}\" for change notifications.", directory);

        // History of the tweaks made to handle file change notifications:
        //
        // Initially, we were using a single watcher watching for DirectoryName, FileName and
        // LastWrite changes, with a buffer of 16KB.
        //
        //  

        // Note: "DirectoryName" captures directory creation, deletion and rename
        watcherEntry.DirectoryNameWatcher.NotifyFilter = NotifyFilters.DirectoryName;
        watcherEntry.DirectoryNameWatcher.Changed += (s, e) => StateHost.ParentWatcher.WatcherOnChanged(s, e, PathKind.Directory);
        watcherEntry.DirectoryNameWatcher.Created += (s, e) => StateHost.ParentWatcher.WatcherOnCreated(s, e, PathKind.Directory);
        watcherEntry.DirectoryNameWatcher.Deleted += (s, e) => StateHost.ParentWatcher.WatcherOnDeleted(s, e, PathKind.Directory);
        watcherEntry.DirectoryNameWatcher.Renamed += (s, e) => StateHost.ParentWatcher.WatcherOnRenamed(s, e, PathKind.Directory);
        watcherEntry.DirectoryNameWatcher.InternalBufferSize = 8 * 1024; // 8 KB

        // Note: "FileName" captures file creation, deletion and rename
        watcherEntry.FileNameWatcher.NotifyFilter = NotifyFilters.FileName;
        watcherEntry.FileNameWatcher.Changed += (s, e) => StateHost.ParentWatcher.WatcherOnChanged(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Created += (s, e) => StateHost.ParentWatcher.WatcherOnCreated(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Deleted += (s, e) => StateHost.ParentWatcher.WatcherOnDeleted(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Renamed += (s, e) => StateHost.ParentWatcher.WatcherOnRenamed(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.InternalBufferSize = 8 * 1024; // 8 KB

        // Note: "LastWrite" will catch changes to *both* files and directories, i.e. it is
        // not possible to known which one it is.
        // For directories, a "LastWrite" change occurs when a child entry (file or directory) is added,
        // renamed or deleted.
        // For files, a "LastWrite" change occurs when the file is written to.
        watcherEntry.FileWriteWatcher.NotifyFilter = NotifyFilters.LastWrite;
        watcherEntry.FileWriteWatcher.Changed += (s, e) => StateHost.ParentWatcher.WatcherOnChanged(s, e, PathKind.FileOrDirectory);
        watcherEntry.FileWriteWatcher.Created += (s, e) => StateHost.ParentWatcher.WatcherOnCreated(s, e, PathKind.FileOrDirectory);
        watcherEntry.FileWriteWatcher.Deleted += (s, e) => StateHost.ParentWatcher.WatcherOnDeleted(s, e, PathKind.FileOrDirectory);
        watcherEntry.FileWriteWatcher.Renamed += (s, e) => StateHost.ParentWatcher.WatcherOnRenamed(s, e, PathKind.FileOrDirectory);
        watcherEntry.FileWriteWatcher.InternalBufferSize = 16 * 1024; // 16 KB

        foreach (var watcher in new[]
          {watcherEntry.DirectoryNameWatcher, watcherEntry.FileNameWatcher, watcherEntry.FileWriteWatcher}) {
          watcher.IncludeSubdirectories = true;
          // Note: The MSDN documentation says to use less than 64KB
          //       (see https://msdn.microsoft.com/en-us/library/system.io.filesystemwatcher.internalbuffersize(v=vs.110).aspx)
          //         "You can set the buffer to 4 KB or larger, but it must not exceed 64 KB."
          //       However, the implementation allows for arbitrary buffer sizes.
          //       Experience has shown that 64KB is small enough that we frequently run into "OverflowException"
          //       exceptions on heavily active file systems (e.g. during a build of a complex project
          //       such as Chromium).
          //       The issue with these exceptions is that the consumer must be extremely conservative
          //       when such errors occur, because we lost track of what happened at the individual
          //       directory/file level. In the case of VsChromium, the server will batch a full re-scan
          //       of the file system, instead of an incremental re-scan, and that can be quite time
          //       consuming (as well as I/O consuming).
          //       In the end, increasing the size of the buffer to 2 MB is the best option to avoid
          //       these issues (2 MB is not that much memory in the grand scheme of things).
          //watcher.InternalBufferSize = 2 * 1024 * 1024; // 2 MB
          //watcher.InternalBufferSize = 60 * 1024; // 16 KB
          watcher.Error += StateHost.ParentWatcher.WatcherOnError;
        }
        OnWatcherAdded(directory, watcherEntry);
      }

      protected void RemoveDirectory(FullPath directory) {
        DirectoryWatcherhEntry watcher;
        lock (StateHost.ParentWatcher._watchersLock) {
          if (!StateHost.ParentWatcher._watchers.TryGetValue(directory, out watcher))
            return;
          StateHost.ParentWatcher._watchers.Remove(directory);
        }
        Logger.LogInfo("Removing directory \"{0}\" from change notification monitoring.", directory);
        watcher.Dispose();
        OnWatcherRemoved(directory, watcher);
      }

      protected void StartWatchers() {
        lock (StateHost.ParentWatcher._watchersLock) {
          foreach (var watcher in StateHost.ParentWatcher._watchers) {
            watcher.Value.Start();
          }
        }
      }

      protected void StopWatchers() {
        lock (StateHost.ParentWatcher._watchersLock) {
          foreach (var watcher in StateHost.ParentWatcher._watchers) {
            watcher.Value.Stop();
          }
        }
      }
    }
  }
}