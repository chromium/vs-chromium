// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;

namespace VsChromium.Core.Files {
  public interface IDirectoryChangeWatcher {
    /// <summary>
    /// Update the list of directories to watch
    /// </summary>
    void WatchDirectories(IEnumerable<FullPath> directories);

    /// <summary>
    /// Pause the underlying file system watchers. The watchers will fully stop so that
    /// they don't consume file system resources when files change on disk.
    /// </summary>
    void Resume();

    /// <summary>
    /// Resume the underlying file system watchers, so that file change notifications
    /// will start to be fired again.
    /// </summary>
    void Pause();

    event Action<IList<PathChangeEntry>> PathsChanged;
    event Action<Exception> Error;
    event Action Paused;
    event Action Resumed;
  }
}
