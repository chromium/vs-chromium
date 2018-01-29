// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabaseSnapshotFactory {
    IFileDatabaseSnapshot CreateEmpty();

    IFileDatabaseSnapshot CreateIncremental(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken);

    IFileDatabaseSnapshot CreateIncrementalWithFileSystemUpdates(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot newFileSystemSnapshot, FullPathChanges fullPathChanges,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken);

    IFileDatabaseSnapshot CreateIncrementalWithModifiedFiles(IFileDatabaseSnapshot previousDatabase,
      FileSystemSnapshot fileSystemSnapshot, IList<ProjectFileName> changedFiles,
      Action onLoading, Action onLoaded,
      Action<IFileDatabaseSnapshot> onIntermadiateResult, CancellationToken cancellationToken);
  }
}