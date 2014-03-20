// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.ComponentModel.Composition;

namespace VsChromium.Server.FileSystem {
  [Export(typeof(IDirectoryChangeWatcherFactory))]
  public class DirectoryChangeWatcherFactory : IDirectoryChangeWatcherFactory {
    public IDirectoryChangeWatcher CreateWatcher() {
      return new DirectoryChangeWatcher();
    }
  }
}
