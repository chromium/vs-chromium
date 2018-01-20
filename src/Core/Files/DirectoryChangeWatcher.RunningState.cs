// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// Initial state: the directory watcher is active.
    /// </summary>
    private class RunningState : State {
      public RunningState(SharedState sharedState) : base(sharedState) {
      }

      public override void OnStateActive() {
        StartWatchers();
      }

      public override State OnPause() {
        StopWatchers();
        SharedState.ParentWatcher.OnPaused();
        return new PausedState(SharedState);
      }

      public override State OnResume() {
        return this;
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
        CheckDeletedRoots();
        PostPathsChangedEvents();
        return this;
      }

      public override State OnWatcherErrorEvent(object sender, ErrorEventArgs args) {
        Logger.LogError(args.GetException(), "File system watcher for path \"{0}\" error.",
          ((FileSystemWatcher)sender).Path);
        StopWatchers();
        SharedState.ParentWatcher.OnError(args.GetException());

        // Automatically pause when we hit a watcher error
        return new ErrorState(SharedState);
      }

      public override State OnWatcherFileChangedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return OnWatcherSingleFileChange(sender, args, pathKind, PathChangeKind.Changed);
      }

      public override State OnWatcherFileCreatedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return OnWatcherSingleFileChange(sender, args, pathKind, PathChangeKind.Created);
      }

      public override State OnWatcherFileDeletedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return OnWatcherSingleFileChange(sender, args, pathKind, PathChangeKind.Deleted);
      }

      public override State OnWatcherFileRenamedEvent(object sender, RenamedEventArgs args, PathKind pathKind) {
        var watcher = (FileSystemWatcher)sender;

        var path = PathHelpers.CombinePaths(watcher.Path, args.Name);
        LogPathForDebugging(path, PathChangeKind.Created, pathKind);
        if (SharedState.ParentWatcher.SkipPath(path))
          return this;

        var oldPath = PathHelpers.CombinePaths(watcher.Path, args.OldName);
        LogPathForDebugging(oldPath, PathChangeKind.Deleted, pathKind);
        if (SharedState.ParentWatcher.SkipPath(oldPath))
          return this;

        SharedState.ParentWatcher.EnqueueChangeEvent(new FullPath(watcher.Path), new RelativePath(args.OldName), PathChangeKind.Deleted, pathKind);
        SharedState.ParentWatcher.EnqueueChangeEvent(new FullPath(watcher.Path), new RelativePath(args.Name), PathChangeKind.Created, pathKind);
        SharedState.ParentWatcher._eventReceived.Set();
        return this;
      }

      public override State OnWatcherAdded(FullPath directory, DirectoryWatcherhEntry watcher) {
        watcher.Start();
        return this;
      }

      public override State OnWatcherRemoved(FullPath directory, DirectoryWatcherhEntry watcher) {
        return this;
      }

      private State OnWatcherSingleFileChange(object sender, FileSystemEventArgs args, PathKind pathKind, PathChangeKind changeKind) {
        var watcher = (FileSystemWatcher)sender;

        var path = PathHelpers.CombinePaths(watcher.Path, args.Name);
        LogPathForDebugging(path, PathChangeKind.Changed, pathKind);
        if (SharedState.ParentWatcher.SkipPath(path))
          return this;

        SharedState.ParentWatcher.EnqueueChangeEvent(new FullPath(watcher.Path), new RelativePath(args.Name),
          changeKind, pathKind);
        SharedState.ParentWatcher._eventReceived.Set();
        return this;
      }

      private void PostPathsChangedEvents() {
        Debug.Assert(SharedState.ParentWatcher._pollingThread == Thread.CurrentThread);
        var changedPaths = SharedState.ParentWatcher.DequeueChangedPathsEvents();

        // Dequeue events as long as there are new ones showing up as we wait for
        // our polling delays to expire.
        // The goal is to delay generating events as long as there is disk
        // activity within a 10 seconds window. It also allows "merging"
        // consecutive events into more meaningful ones.
        while (changedPaths.Count > 0) {

          // Post changes that belong to an expired polling interval.
          if (SharedState.ParentWatcher._simplePathChangesPolling.WaitTimeExpired()) {
            SharedState.ParentWatcher.PostPathsChangedEvents(changedPaths, x => x == PathChangeKind.Changed);
            SharedState.ParentWatcher._simplePathChangesPolling.Restart();
          }
          if (SharedState.ParentWatcher._pathChangesPolling.WaitTimeExpired()) {
            SharedState.ParentWatcher.PostPathsChangedEvents(changedPaths, x => true);
            SharedState.ParentWatcher._pathChangesPolling.Restart();
          }

          // If we are done, exit to waiting thread
          if (changedPaths.Count == 0)
            break;

          // If there are leftover paths, this means some polling interval(s) have
          // not expired. Go back to sleeping for a little bit or until we receive
          // a new change event.
          SharedState.ParentWatcher._eventReceived.WaitOne(SharedState.ParentWatcher._pollingThreadTimeout);

          // See if we got new events, and merge them.
          var morePathsChanged = SharedState.ParentWatcher.DequeueChangedPathsEvents();
          morePathsChanged.ForAll(change => MergePathChange(changedPaths, change.Value));

          // If we got more changes, reset the polling interval for the non-simple
          // path changed. The goal is to avoid processing those too frequently if
          // there is activity on disk (e.g. a build happening), because
          // processing add/delete changes is currently much more expensive in the
          // search engine file database.
          // Note we don't update the simple "file change" events, as those as cheaper
          // to process.
          if (morePathsChanged.Count > 0) {
            SharedState.ParentWatcher._simplePathChangesPolling.Checkpoint();
            SharedState.ParentWatcher._pathChangesPolling.Checkpoint();
          }
        }

        // We are done processing all changes, make sure we wait at least some
        // amount of time before processing anything more.
        SharedState.ParentWatcher._simplePathChangesPolling.Restart();
        SharedState.ParentWatcher._pathChangesPolling.Restart();

        Debug.Assert(changedPaths.Count == 0);
      }

      /// <summary>
      /// The OS FileSystem notification does not notify us if a directory used for
      /// change notification is deleted (or renamed). We have to use polling to detect
      /// this kind of changes.
      /// </summary>
      private void CheckDeletedRoots() {
        Debug.Assert(SharedState.ParentWatcher._pollingThread == Thread.CurrentThread);
        if (!SharedState.ParentWatcher._checkRootsPolling.WaitTimeExpired())
          return;
        SharedState.ParentWatcher._checkRootsPolling.Restart();

        lock (SharedState.ParentWatcher._watchersLock) {
          var deletedWatchers = SharedState.ParentWatcher._watchers
            .Where(item => !SharedState.ParentWatcher._fileSystem.DirectoryExists(item.Key))
            .ToList();

          deletedWatchers
            .ForAll(item => {
              SharedState.ParentWatcher.EnqueueChangeEvent(item.Key, RelativePath.Empty, PathChangeKind.Deleted, PathKind.Directory);
              RemoveDirectory(item.Key);
            });
        }
      }
    }
  }
}