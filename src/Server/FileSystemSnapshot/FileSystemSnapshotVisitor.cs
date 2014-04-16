// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Server.FileSystemSnapshot {
  /// <summary>
  /// A simple BFS visitor of a <see cref="FileSystemTreeSnapshot"/> instance.
  /// </summary>
  public class FileSystemSnapshotVisitor {
    private readonly FileSystemTreeSnapshot _snapshot;

    public FileSystemSnapshotVisitor(FileSystemTreeSnapshot snapshot) {
      _snapshot = snapshot;
    }

    /// <summary>
    /// Called for each directory entry of the snapshot.
    /// </summary>
    public Action<ProjectRootSnapshot, DirectorySnapshot> VisitDirectory { get; set; }

    /// <summary>
    /// Called for each file of the snapshot.
    /// </summary>
    public Action<ProjectRootSnapshot, FileName> VisitFile { get; set; }

    /// <summary>
    /// Visits all directory and files, calling <see cref="VisitFile"/> and <see
    /// cref="VisitDirectory"/> appropriately.
    /// </summary>
    public void Visit() {
      _snapshot.ProjectRoots.ForAll(x => VisitWorker(x, x.Directory));
    }

    private void VisitWorker(ProjectRootSnapshot project, DirectorySnapshot directory) {
      VisitDirectory(project, directory);
      directory.Files.ForAll(x => VisitFile(project, x));
      directory.DirectoryEntries.ForAll(x => VisitWorker(project, x));
    }
  }
}
