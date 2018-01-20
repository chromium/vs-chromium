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

      public ErrorState(StateHost stateHost) : base(stateHost) {
        _enteredStateUtc = stateHost.ParentWatcher._dateTimeProvider.UtcNow;
      }

      public override State OnResume() {
        StartWatchers();
        StateHost.ParentWatcher.OnResumed();
        return new RunningState(StateHost);
      }

      public override State OnPause() {
        return new PausedState(StateHost);
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
        // Check how long we have been in error state.
        // If it is longer than "autoRestartDelay", enter the "restarting" state
        if (StateHost.ParentWatcher._autoRestartDelay != null) {
          var span = DateTimeProvider.UtcNow - _enteredStateUtc;
          if (span >= StateHost.ParentWatcher._autoRestartDelay.Value) {
            return new RestartingState(StateHost);
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