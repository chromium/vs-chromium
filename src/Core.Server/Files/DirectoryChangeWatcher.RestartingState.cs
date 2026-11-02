// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class RestartingState : State {
      private static readonly bool _conservative = false;
      /// <summary>
      /// The date/time when we restarted watching files, but still observing disk activity before
      /// resuming notifications.
      /// </summary>
      private readonly DateTime? _enteredStateUtc;

      public RestartingState(StateHost stateHost) : base(stateHost) {
        _enteredStateUtc = DateTimeProvider.UtcNow;
      }

      public override void OnStateActive() {
        StartWatchers();
      }

      public override State OnResume() {
        StateHost.ParentWatcher.OnResumed();
        return new RunningState(StateHost);
      }

      public override State OnPause() {
        StopWatchers();
        return new PausedState(StateHost);
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        WatchDirectoriesImpl(directories);
        return this;
      }

      public override State OnPolling() {
        var span = DateTimeProvider.UtcNow - _enteredStateUtc;
        if (span > StateHost.ParentWatcher._autoRestartObservePeriod) {
          // We have not had any events so we can restart evertything
          StateHost.ParentWatcher.OnResumed();
          return new RunningState(StateHost);
        }
        return this;
      }

      public override State OnWatcherErrorEvent(object sender, ErrorEventArgs args) {
        // If there is another buffer overflow, go straight back to the error state,
        // don't ever be conservative.
        return BackToErrorState();
      }

      public override State OnWatcherFileChangedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return BackToState();
      }

      public override State OnWatcherFileCreatedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return BackToState();
      }

      public override State OnWatcherFileDeletedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return BackToState();
      }

      public override State OnWatcherFileRenamedEvent(object sender, RenamedEventArgs args, PathKind pathKind) {
        return BackToState();
      }

      public override State OnWatcherAdded(FullPath directory, DirectoryWatcherhEntry watcher) {
        return BackToState();
      }

      public override State OnWatcherRemoved(FullPath directory, DirectoryWatcherhEntry watcher) {
        return BackToState();
      }

      private State BackToState() {
        return _conservative ? BackToErrorState() : this;
      }

      private State BackToErrorState() {
        StopWatchers();
        return new ErrorState(StateHost);
      }
    }
  }
}