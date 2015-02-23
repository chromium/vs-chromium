// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystemDatabase {
  public interface IFileDatabaseFactory {
    IFileDatabase CreateEmpty();

    IFileDatabase CreateIncremental(IFileDatabase previousFileDatabase, FileSystemTreeSnapshot newSnapshot);

    IFileDatabase CreateWithChangedFiles(
      IFileDatabase previousFileDatabase,
      IEnumerable<Tuple<IProject, FileName>> changedFiles,
      Action onLoading,
      Action onLoaded);
  }
}