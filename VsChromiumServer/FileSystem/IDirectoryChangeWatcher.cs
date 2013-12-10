// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromiumServer.FileSystemNames;

namespace VsChromiumServer.FileSystem {
  public interface IDirectoryChangeWatcher {
    void WatchDirectories(IEnumerable<DirectoryName> directories);

    event Action<IList<KeyValuePair<string, ChangeType>>> PathsChanged;
  }

  public enum ChangeType : byte {
    None,
    Created,
    Deleted,
    Changed
  }
}
