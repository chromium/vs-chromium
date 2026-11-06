// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Threading;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.Projects;

namespace VsChromium.Server.FileSystem {
  public interface IFileSystemSnapshotBuilder {
    FileSystemSnapshot Compute(
      IFileSystemNameFactory fileNameFactory, 
      FileSystemSnapshot oldSnapshot,
      FullPathChanges pathChanges /* may be null */,
      IList<IProject> projects, 
      int version,
      CancellationToken cancellationToken,
      SnapshotComputeOptions options = null);
  }

  /// <summary>The projects of a <see cref="IFileSystemSnapshotBuilder.Compute"/> computed differently.</summary>
  public sealed class SnapshotComputeOptions {
    /// <summary>The trees of projects not to scan (index cache), with their change journal checkpoint.</summary>
    public IReadOnlyDictionary<FullPath, (DirectorySnapshot Root, ChangeJournalCheckpoint Checkpoint)> PrecomputedRoots { get; set; }

    /// <summary>Projects to scan entirely, even if path changes are given.</summary>
    public ISet<FullPath> FullScanRoots { get; set; }

    /// <summary>
    /// Projects whose tree (in the old snapshot) comes from the index cache, to verify: from the
    /// change journal since their checkpoint (catch-up) if possible, otherwise by a full scan. The
    /// result of each verification is in <see cref="Verifications"/>.
    /// </summary>
    public IReadOnlyDictionary<FullPath, ChangeJournalCheckpoint> VerifyRoots { get; set; }

    /// <summary>The verifications of <see cref="VerifyRoots"/> (output).</summary>
    public Dictionary<FullPath, ProjectVerification> Verifications { get; } = new Dictionary<FullPath, ProjectVerification>();
  }
}
