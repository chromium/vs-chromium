// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystem;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabaseFactory {
    IFileDatabaseSnapshot CreateEmpty();

    IFileDatabaseSnapshot CreateIncremental(
      IFileDatabaseSnapshot previousFileDatabaseSnapshot,
      FileSystemSnapshot previousSnapshot,
      FileSystemSnapshot newSnapshot,
      FullPathChanges fullPathChanges,
      Action<IFileDatabaseSnapshot> onIntermadiateResult);

    IFileDatabaseSnapshot CreateWithChangedFiles(
      IFileDatabaseSnapshot previousFileDatabaseSnapshot,
      IEnumerable<ProjectFileName> changedFiles,
      Action onLoading,
      Action onLoaded);
  }
}