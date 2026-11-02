// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using VsChromium.Core.Logging;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// State reached when file system watchers can't be used because of an
    /// operating system resource limit (e.g. the inotify watch limit,
    /// <c>fs.inotify.max_user_watches</c>, on Linux): retrying is pointless,
    /// so the watchers are stopped and <see cref="RescanRequired"/> is fired
    /// periodically (<see cref="PollingFallbackPeriod"/>), so that the consumer
    /// rescans the directories to find out what changed.
    /// </summary>
    private class PollingFallbackState : State {
      private DateTime _lastRescanUtc;

      public PollingFallbackState(StateHost stateHost, Exception error) : base(stateHost) {
        _lastRescanUtc = DateTimeProvider.UtcNow;
        Logger.LogWarn(error,
          "File system change notifications are not available (operating system limit reached, " +
          "e.g. \"fs.inotify.max_user_watches\" on Linux): falling back to rescanning the file system " +
          "every {0:n0} seconds.",
          StateHost.ParentWatcher.PollingFallbackPeriod.TotalSeconds);
        StopWatchers();
      }

      public override State OnResume() {
        // Already "running" (in polling mode).
        return this;
      }

      public override State OnPause() {
        StateHost.ParentWatcher.OnPaused();
        return new PausedState(StateHost);
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        // Keep track of the directories (watchers are created but never started in this state).
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
        var now = DateTimeProvider.UtcNow;
        if (now - _lastRescanUtc >= StateHost.ParentWatcher.PollingFallbackPeriod) {
          _lastRescanUtc = now;
          StateHost.ParentWatcher.OnRescanRequired();
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
        // Don't start the watcher.
        return this;
      }

      public override State OnWatcherRemoved(FullPath directory, DirectoryWatcherhEntry watcher) {
        return this;
      }
    }
  }
}
