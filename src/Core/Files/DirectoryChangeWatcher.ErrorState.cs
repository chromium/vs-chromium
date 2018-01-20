// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// State reached when a file system watcher error occured.
    /// </summary>
    private class ErrorState : State {
      private readonly DateTime _enteredStateUtc;

      public ErrorState(SharedState sharedState) : base(sharedState) {
        _enteredStateUtc = sharedState.ParentWatcher._dateTimeProvider.UtcNow;
      }

      public override State OnResume() {
        StartWatchers();
        SharedState.ParentWatcher.OnResumed();
        return new RunningState(SharedState);
      }

      public override State OnPause() {
        return new PausedState(SharedState);
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
        // Check how long we have been in error state.
        // If it is longer than "autoRestartDelay", enter the "restarting" state
        if (SharedState.ParentWatcher._autoRestartDelay != null) {
          var span = SharedState.ParentWatcher._dateTimeProvider.UtcNow - _enteredStateUtc;
          if (span >= SharedState.ParentWatcher._autoRestartDelay.Value) {
            return new RestartingState(SharedState);
          }
        }

        return this;
      }

      public override State OnWatcherErrorEvent(object sender, ErrorEventArgs args) {
        return this;
      }

      public override State OnWatcherFileChangedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return this;
      }

      public override State OnWatcherFileCreatedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return this;
      }

      public override State OnWatcherFileDeletedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return this;
      }

      public override State OnWatcherFileRenamedEvent(object sender, RenamedEventArgs args, PathKind pathKind) {
        return this;
      }

      public override State OnWatcherAdded(FullPath directory, DirectoryWatcherhEntry watcher) {
        return this;
      }

      public override State OnWatcherRemoved(FullPath directory, DirectoryWatcherhEntry watcher) {
        return this;
      }
    }
  }
}