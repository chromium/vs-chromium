// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabaseSnapshotFactory {
    IFileDatabaseSnapshot CreateEmpty();

    IFileDatabaseSnapshot CreateIncremental(
      IFileDatabaseSnapshot previousSnapshot,
      FileSystemSnapshot newFileSystemSnapshot,
      FullPathChanges fullPathChanges,
      Action<IFileDatabaseSnapshot> onIntermadiateResult);

    IFileDatabaseSnapshot CreateWithChangedFiles(
      IFileDatabaseSnapshot previousSnapshot,
      IEnumerable<ProjectFileName> changedFiles,
      Action onLoading,
      Action onLoaded);
  }
}