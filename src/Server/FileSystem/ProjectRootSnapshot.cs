// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Core.Files;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public class ProjectRootSnapshot {
    private readonly IProject _project;
    private readonly DirectorySnapshot _directory;
    private readonly ChangeJournalCheckpointHolder _checkpoint;

    public ProjectRootSnapshot(IProject project, DirectorySnapshot directory, ChangeJournalCheckpointHolder checkpoint = null) {
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
    public ChangeJournalCheckpoint Checkpoint { get { return _checkpoint?.Value; } }

    /// <summary>
    /// The checkpoint of the tree, shared by the snapshots of the incremental updates of the same
    /// scan (see <see cref="ChangeJournalCheckpointHolder"/>), or null.
    /// </summary>
    public ChangeJournalCheckpointHolder CheckpointHolder { get { return _checkpoint; } }
  }

  /// <summary>
  /// The change journal checkpoint of the tree of a project, created by a scan (full scan: the end
  /// of the journal before the scan; catch-up: the end of the journal before the directories are
  /// listed again), and shared by the snapshots of the incremental updates that follow: they
  /// contain all the changes before it too (they only add later changes). Advanced by the journal
  /// self-check of the saves of the index cache (see <c>ChangeJournalSelfCheck</c>), so that the
  /// next self-check and the next start read the journal from there. Thread safe.
  /// </summary>
  public sealed class ChangeJournalCheckpointHolder {
    private volatile ChangeJournalCheckpoint _value;

    public ChangeJournalCheckpointHolder(ChangeJournalCheckpoint value) {
      _value = value;
    }

    public ChangeJournalCheckpoint Value => _value;

    /// <summary>
    /// Advances the checkpoint to <paramref name="value"/> if it is a later position of the same
    /// journal (never backwards, never from no checkpoint).
    /// </summary>
    public void Advance(ChangeJournalCheckpoint value) {
      lock (this) {
        var current = _value;
        if (current != null && current.IsSameJournal(value) && value.Usn > current.Usn) {
          _value = value;
        }
      }
    }
  }
}
