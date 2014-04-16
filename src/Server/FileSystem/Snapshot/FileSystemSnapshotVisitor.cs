// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystem.Snapshot {
  /// <summary>
  /// A simple BFS visitor of a <see cref="FileSystemSnapshot"/> instance.
  /// </summary>
  public class FileSystemSnapshotVisitor {
    private readonly FileSystemSnapshot _snapshot;

    public FileSystemSnapshotVisitor(FileSystemSnapshot snapshot) {
      _snapshot = snapshot;
    }

    /// <summary>
    /// Called for each directory entry of the snapshot.
    /// </summary>
    public Action<DirectorySnapshot> VisitDirectory { get; set; }

    /// <summary>
    /// Called for each file of the snapshot.
    /// </summary>
    public Action<FileName> VisitFile { get; set; }

    /// <summary>
    /// Visits all directory and files, calling <see cref="VisitFile"/> and <see
    /// cref="VisitDirectory"/> appropriately.
    /// </summary>
    public void Visit() {
      _snapshot.ProjectRoots.ForAll(x => VisitWorker(x.Directory));
    }

    private void VisitWorker(DirectorySnapshot entry) {
      VisitDirectory(entry);
      entry.Files.ForAll(x => VisitFile(x));
      entry.DirectoryEntries.ForAll(x => VisitWorker(x));
    }
  }
}
