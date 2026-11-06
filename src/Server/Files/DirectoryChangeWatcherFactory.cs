// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  public class DirectoryChangeWatcherFactory : IDirectoryChangeWatcherFactory {
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly TimeSpan? _changeDelay;

    /// <param name="changeDelay">The delay of the processing of the changes (see
    /// <see cref="DirectoryChangeWatcher.ChangeDelay"/>), null for the default delays.</param>
    public DirectoryChangeWatcherFactory(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider, TimeSpan? changeDelay = null) {
      _fileSystem = fileSystem;
      _dateTimeProvider = dateTimeProvider;
      _changeDelay = changeDelay;
    }

    public IDirectoryChangeWatcher CreateWatcher(TimeSpan? autoRestartDelay) {
      return new DirectoryChangeWatcher(_fileSystem, _dateTimeProvider, autoRestartDelay) { ChangeDelay = _changeDelay };
    }
  }
}
