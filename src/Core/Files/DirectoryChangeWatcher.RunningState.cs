// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// Initial state: the directory watcher is active.
    /// </summary>
    private class RunningState : State {
      private readonly PollingDelayPolicy _pathChangesPolling;
      private readonly PollingDelayPolicy _simplePathChangesPolling;
      private readonly PollingDelayPolicy _checkRootsPolling;

      /// <summary>
      /// Change events that are coming from the underlying file change notification events, but have not
      /// been procseed and buffered yet.
      /// </summary>
      private Dictionary<FullPath, PathChangeEntry> _enqueuedChangedPaths = new Dictionary<FullPath, PathChangeEntry>();

      /// <summary>
      /// Change events that have been moved and merged from <see cref="_enqueuedChangedPaths"/>, but have not
      /// been posted as events to our consumer.
      /// </summary>
      private readonly IDictionary<FullPath, PathChangeEntry> _bufferedChangedPaths = new Dictionary<FullPath, PathChangeEntry>();

      public RunningState(StateHost stateHost) : base(stateHost) {
        _simplePathChangesPolling = new PollingDelayPolicy(DateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(10.0));
        _pathChangesPolling = new PollingDelayPolicy(DateTimeProvider, TimeSpan.FromSeconds(2.0), TimeSpan.FromSeconds(60.0));
        _checkRootsPolling = new PollingDelayPolicy(DateTimeProvider, TimeSpan.FromSeconds(15.0), TimeSpan.FromSeconds(60.0));
      }

      public override void OnStateActive() {
        StartWatchers();
      }

      public override State OnPause() {
        StopWatchers();
        StateHost.ParentWatcher.OnPaused();
        return new PausedState(StateHost);
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
        ProcessChangedPathEvents();
        return this;
      }

      public override State OnWatcherErrorEvent(object sender, ErrorEventArgs args) {
        Logger.LogError(args.GetException(), "File system watcher for path \"{0}\" error.",
          ((IFileSystemWatcher)sender).Path);
        StopWatchers();
        StateHost.ParentWatcher.OnError(args.GetException());

        // Automatically pause when we hit a watcher error
        return new ErrorState(StateHost);
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
        var watcher = (IFileSystemWatcher)sender;

        var path = PathHelpers.CombinePaths(watcher.Path.Value, args.Name);
        LogPathForDebugging(path, PathChangeKind.Created, pathKind);
        if (SkipPath(path))
          return this;

        var oldPath = PathHelpers.CombinePaths(watcher.Path.Value, args.OldName);
        LogPathForDebugging(oldPath, PathChangeKind.Deleted, pathKind);
        if (SkipPath(oldPath))
          return this;

        EnqueueChangeEvent(watcher.Path, new RelativePath(args.OldName), PathChangeKind.Deleted, pathKind);
        EnqueueChangeEvent(watcher.Path, new RelativePath(args.Name), PathChangeKind.Created, pathKind);
        StateHost.PollingThread.WakeUp();
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
        var watcher = (IFileSystemWatcher)sender;

        var path = PathHelpers.CombinePaths(watcher.Path.Value, args.Name);
        LogPathForDebugging(path, PathChangeKind.Changed, pathKind);
        if (SkipPath(path))
          return this;

        EnqueueChangeEvent(watcher.Path, new RelativePath(args.Name), changeKind, pathKind);
        StateHost.PollingThread.WakeUp();
        return this;
      }

      private void ProcessChangedPathEvents() {
        Debug.Assert(StateHost.PollingThread.IsThread(Thread.CurrentThread));
        var morePathsChanged = DequeueChangedPathsEvents();

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

        // Merge the new events into the existing events
        morePathsChanged.ForAll(change => MergePathChange(_bufferedChangedPaths, change.Value));

        // Post changes that belong to an expired polling interval.
        if (_simplePathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(_bufferedChangedPaths, x => x == PathChangeKind.Changed);
          _simplePathChangesPolling.Restart();
        }
        if (_pathChangesPolling.WaitTimeExpired()) {
          PostPathsChangedEvents(_bufferedChangedPaths, x => true);
          _pathChangesPolling.Restart();
        }

        if (_bufferedChangedPaths.Count == 0) {
          // We are done processing all changes, make sure we wait at least some
          // amount of time before processing anything more.
          _simplePathChangesPolling.Restart();
          _pathChangesPolling.Restart();
        }
      }

      /// <summary>
      /// The OS FileSystem notification does not notify us if a directory used for
      /// change notification is deleted (or renamed). We have to use polling to detect
      /// this kind of changes.
      /// </summary>
      private void CheckDeletedRoots() {
        Debug.Assert(StateHost.PollingThread.IsThread(Thread.CurrentThread));
        if (!_checkRootsPolling.WaitTimeExpired())
          return;
        _checkRootsPolling.Restart();

        var deletedWatchers = StateHost.WatcherDictionary
          .Where(item => !StateHost.ParentWatcher._fileSystem.DirectoryExists(item.Key))
          .ToList();

        deletedWatchers
          .ForAll(item => {
            EnqueueChangeEvent(item.Key, RelativePath.Empty, PathChangeKind.Deleted, PathKind.Directory);
            RemoveDirectory(item.Key);
          });
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
          StateHost.ParentWatcher.OnPathsChanged(changes);
        }
      }

      private void RemoveIgnorableEvents(IDictionary<FullPath, PathChangeEntry> changes) {
        changes.RemoveWhere(x => !IncludeChange(x.Value));
      }

      private bool IncludeChange(PathChangeEntry entry) {
        // Ignore changes for files that have been created then deleted
        if (entry.ChangeKind == PathChangeKind.None)
          return false;

        return true;
      }

      private IDictionary<FullPath, PathChangeEntry> DequeueChangedPathsEvents() {
        // Copy current changes into temp and reset to empty collection.
        var temp = _enqueuedChangedPaths;
        _enqueuedChangedPaths = new Dictionary<FullPath, PathChangeEntry>();
        return temp;
      }

      private void EnqueueChangeEvent(FullPath rootPath, RelativePath entryPath, PathChangeKind changeKind, PathKind pathKind) {
        //Logger.LogInfo("Enqueue change event: {0}, {1}", path, changeKind);
        var entry = new PathChangeEntry(rootPath, entryPath, changeKind, pathKind);
        GlobalChangeRecorder.RecordChange(new PathChangeRecorder.ChangeInfo {
          Entry = entry,
          TimeStampUtc = DateTimeProvider.UtcNow,
        });

        MergePathChange(_enqueuedChangedPaths, entry);
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
          switch (StateHost.LogLimiter.Proceed()) {
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
          switch (StateHost.LogLimiter.Proceed()) {
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
}