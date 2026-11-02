// Copyright 2018 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;

namespace VsChromium.Core.Files {
  public partial class DirectoryChangeWatcher {
    private class DisposedState : State {
      public DisposedState(StateHost stateHost) : base(stateHost) {
      }

      public override State OnResume() {
        throw new ObjectDisposedException("Object is disposed");
      }

      public override State OnPause() {
        throw new ObjectDisposedException("Object is disposed");
      }

      public override State OnWatchDirectories(IEnumerable<FullPath> directories) {
        throw new ObjectDisposedException("Object is disposed");
      }

      public override State OnPolling() {
        return this;
      }

      public override State OnWatcherErrorEvent(object sender, ErrorEventArgs args) {
        // This should not happen really, as we should be inactive
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

      public override State OnDispose() {
        throw new ObjectDisposedException("Object has already been disposed");
      }

      public override State OnWatcherFileChangedEvent(object sender, FileSystemEventArgs args, PathKind pathKind) {
        return this;
      }
    }
  }
}