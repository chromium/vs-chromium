// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.FileSystemSnapshot;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemProcessor {
    void AddFile(string filename);
    void RemoveFile(string filename);
    FileSystemTreeSnapshot GetCurrentSnapshot();

    event SnapshotComputingDelegate SnapshotComputing;
    event SnapshotComputedDelegate SnapshotComputed;
    event FilesChangedDelegate FilesChanged;
  }

  public delegate void SnapshotComputingDelegate(long operationId);
  public delegate void SnapshotComputedDelegate(long operationId, FileSystemTreeSnapshot previousSnapshot, FileSystemTreeSnapshot newSnapshot);
  public delegate void FilesChangedDelegate(IEnumerable<Tuple<IProject, FileName>> changedFiles);
}
