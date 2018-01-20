// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    /// <summary>
    /// State where the directory watcher has been manually paused (by the end-user for example)
    /// </summary>
    private class PausedState : State {
      public PausedState(SharedState sharedState) : base(sharedState) {
      }

      public override State OnResume() {
        StartWatchers();
        SharedState.ParentWatcher.OnResumed();
        return new RunningState(SharedState);
      }

      public override State OnPause() {
        return this;
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
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