// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// The common base class of all possible states
    /// </summary>
    /// <summary>
    /// Size of the buffer of each OS file system watcher: 64 KB, the maximum recommended, so that
    /// mass changes (e.g. a "git checkout") rarely overflow it (an overflow leads to a full rescan).
    /// Environment variable VSCHROMIUM_WATCHER_BUFFER_SIZE (tests: small values force overflows).
    /// </summary>
    private static readonly int WatcherBufferSize = GetWatcherBufferSize();

    private static int GetWatcherBufferSize() {
      int size;
      var value = Environment.GetEnvironmentVariable("VSCHROMIUM_WATCHER_BUFFER_SIZE");
      if (!string.IsNullOrEmpty(value) && int.TryParse(value, out size) && size >= 4096)
        return size;
      return 64 * 1024;
    }

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
        var oldSet = new HashSet<FullPath>(StateHost.WatcherDictionary.Keys);
        var newSet = new HashSet<FullPath>(directories);

        var removed = new HashSet<FullPath>(oldSet);
        removed.ExceptWith(newSet);

        var added = new HashSet<FullPath>(newSet);
        added.ExceptWith(oldSet);

        removed.ForAll(RemoveDirectory);
        added.ForAll(AddDirectory);
      }

      protected void AddDirectory(FullPath directory) {
        DirectoryWatcherhEntry watcherEntry;
        StateHost.PollingThread.Start();
        if (StateHost.WatcherDictionary.TryGetValue(directory, out watcherEntry))
          return;

        if (!OperatingSystem.IsWindows()) {
          AddDirectorySingleWatcher(directory);
          return;
        }

        watcherEntry = new DirectoryWatcherhEntry {
          Path = directory,
          DirectoryNameWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
          FileNameWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
          FileWriteWatcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory),
        };        StateHost.WatcherDictionary.Add(directory, watcherEntry);

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
        watcherEntry.DirectoryNameWatcher.InternalBufferSize = WatcherBufferSize;

        // Note: "FileName" captures file creation, deletion and rename
        watcherEntry.FileNameWatcher.NotifyFilter = NotifyFilters.FileName;
        watcherEntry.FileNameWatcher.Changed += (s, e) => StateHost.ParentWatcher.WatcherOnChanged(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Created += (s, e) => StateHost.ParentWatcher.WatcherOnCreated(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Deleted += (s, e) => StateHost.ParentWatcher.WatcherOnDeleted(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.Renamed += (s, e) => StateHost.ParentWatcher.WatcherOnRenamed(s, e, PathKind.File);
        watcherEntry.FileNameWatcher.InternalBufferSize = WatcherBufferSize;

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
        watcherEntry.FileWriteWatcher.InternalBufferSize = WatcherBufferSize;

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
        if (!StateHost.WatcherDictionary.TryGetValue(directory, out watcher))
          return;
        StateHost.WatcherDictionary.Remove(directory);
        Logger.LogInfo("Removing directory \"{0}\" from change notification monitoring.", directory);
        watcher.Dispose();
        OnWatcherRemoved(directory, watcher);
      }

      /// <summary>
      /// Linux and macOS: a single watcher per directory, for all kinds of
      /// changes. On Linux, each watcher (with sub-directories) uses one inotify
      /// watch per directory of the tree, and the number of watches is limited
      /// (fs.inotify.max_user_watches), so using 3 watchers (as on Windows,
      /// where they are cheap) would divide the size of the trees we can watch
      /// by 3. The drawback is that the kind of path (file or directory) is not
      /// known for change events (PathKind.FileOrDirectory), which is handled
      /// by the consumers (more conservative processing).
      /// </summary>
      private void AddDirectorySingleWatcher(FullPath directory) {
        var watcher = StateHost.ParentWatcher._fileSystem.CreateDirectoryWatcher(directory);
        var watcherEntry = new DirectoryWatcherhEntry {
          Path = directory,
          FileWriteWatcher = watcher,
        };
        StateHost.WatcherDictionary.Add(directory, watcherEntry);

        Logger.LogInfo("Starting monitoring directory \"{0}\" for change notifications (single watcher).", directory);

        watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName | NotifyFilters.LastWrite;
        watcher.Changed += (s, e) => StateHost.ParentWatcher.WatcherOnChanged(s, e, PathKind.FileOrDirectory);
        watcher.Created += (s, e) => StateHost.ParentWatcher.WatcherOnCreated(s, e, PathKind.FileOrDirectory);
        watcher.Deleted += (s, e) => StateHost.ParentWatcher.WatcherOnDeleted(s, e, PathKind.FileOrDirectory);
        watcher.Renamed += (s, e) => StateHost.ParentWatcher.WatcherOnRenamed(s, e, PathKind.FileOrDirectory);
        watcher.InternalBufferSize = WatcherBufferSize;
        watcher.IncludeSubdirectories = true;
        watcher.Error += StateHost.ParentWatcher.WatcherOnError;
        OnWatcherAdded(directory, watcherEntry);
      }
      protected void StartWatchers() {
        foreach (var watcher in StateHost.WatcherDictionary) {
          watcher.Value.Start();
        }
      }

      protected void StopWatchers() {
        foreach (var watcher in StateHost.WatcherDictionary) {
          watcher.Value.Stop();
        }
      }
    }
  }
}
