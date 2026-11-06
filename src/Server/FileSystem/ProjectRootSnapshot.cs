// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class ProjectRootSnapshot {
    private readonly IProject _project;
    private readonly DirectorySnapshot _directory;

    private readonly ChangeJournalCheckpoint _checkpoint;

    public ProjectRootSnapshot(IProject project, DirectorySnapshot directory, ChangeJournalCheckpoint checkpoint = null) {
      _project = project;
      _directory = directory;
      _checkpoint = checkpoint;
    }

    public IProject Project { get { return _project; } }
    public DirectorySnapshot Directory { get { return _directory; } }

    /// <summary>
    /// The position in the change journal of the volume of the project such that every change made
    /// before it is in <see cref="Directory"/> (except the changes of the targets of symbolic links,
    /// always checked again), or null if unknown: saved with the tree in the index cache.
    /// </summary>
    public ChangeJournalCheckpoint Checkpoint { get { return _checkpoint; } }
  }
}
