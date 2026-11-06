// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;

namespace VsChromium.Server.FileSystem {
  public enum VerificationMethod {
    /// <summary>The project was scanned entirely.</summary>
    FullScan,
    /// <summary>Only the directories with records in the change journal were listed again.</summary>
    ChangeJournal,
  }

  /// <summary>
  /// How the tree of the index cache of a project was verified (logs, statistics, dashboard): from
  /// the change journal (catch-up), or by a full scan, and why.
  /// </summary>
  public sealed class ProjectVerification {
    public VerificationMethod Method { get; set; }
    /// <summary>Why the change journal was not used (full scan), or null.</summary>
    public string FallbackReason { get; set; }
    /// <summary>The records of the change journal read.</summary>
    public long RecordCount { get; set; }
    /// <summary>The directories listed again (change journal).</summary>
    public int ListedDirectoryCount { get; set; }
    /// <summary>The new or replaced directories scanned entirely (change journal).</summary>
    public int ScannedSubtreeCount { get; set; }
    /// <summary>The directory symbolic links and junctions scanned entirely (change journal).</summary>
    public int SymLinkSubtreeCount { get; set; }
    /// <summary>The file symbolic links checked again (change journal).</summary>
    public int SymLinkFileCount { get; set; }
    /// <summary>The time of the scan or of the catch-up (journal read included).</summary>
    public double ElapsedMs { get; set; }
    /// <summary>
    /// The change journal checkpoint of the cache must be updated, even if the tree is unchanged:
    /// after a full scan with a new checkpoint, or a catch-up that read more than half of the
    /// journal (the checkpoint of the cache would be overwritten soon: the next start would scan
    /// everything).
    /// </summary>
    public bool CheckpointNeedsSave { get; set; }

    public override string ToString() {
      var result = new StringBuilder();
      if (Method == VerificationMethod.ChangeJournal) {
        result.AppendFormat("change journal: {0:n0} directories listed again in {1:n0} ms ({2:n0} records, " +
                            "{3:n0} new directories and {4:n0} directory symbolic links scanned, {5:n0} file symbolic links checked)",
          ListedDirectoryCount, ElapsedMs, RecordCount, ScannedSubtreeCount, SymLinkSubtreeCount, SymLinkFileCount);
      } else {
        result.AppendFormat("full scan in {0:n0} ms: {1}", ElapsedMs, FallbackReason ?? "change journal not used");
      }
      return result.ToString();
    }
  }
}
