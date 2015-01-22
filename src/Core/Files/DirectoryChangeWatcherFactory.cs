// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;
using VsChromium.Core.Threads;

namespace VsChromium.Core.Files {
  [Export(typeof(IDirectoryChangeWatcherFactory))]
  public class DirectoryChangeWatcherFactory : IDirectoryChangeWatcherFactory {
    private readonly IFileSystem _fileSystem;
    private readonly IDateTimeProvider _dateTimeProvider;

    [ImportingConstructor]
    public DirectoryChangeWatcherFactory(IFileSystem fileSystem, IDateTimeProvider dateTimeProvider) {
      _fileSystem = fileSystem;
      _dateTimeProvider = dateTimeProvider;
    }

    public IDirectoryChangeWatcher CreateWatcher() {
      return new DirectoryChangeWatcher(_fileSystem, _dateTimeProvider);
    }
  }
}
