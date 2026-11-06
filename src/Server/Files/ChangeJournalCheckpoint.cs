// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Core.Files {
  /// <summary>
  /// A position in the change journal of a volume (NTFS/ReFS USN journal, see <see
  /// cref="IChangeJournal"/>): the volume serial number, the id of the journal (a new journal has
  /// a new id) and a USN (update sequence number). Saved with the tree of a project in its index
  /// cache: every change made before <see cref="Usn"/> is in the tree, so that the next start
  /// only lists again the directories with a record after it.
  /// </summary>
  public sealed class ChangeJournalCheckpoint : IEquatable<ChangeJournalCheckpoint> {
    public ChangeJournalCheckpoint(ulong volumeSerialNumber, ulong journalId, long usn) {
      VolumeSerialNumber = volumeSerialNumber;
      JournalId = journalId;
      Usn = usn;
    }

    public ulong VolumeSerialNumber { get; }
    public ulong JournalId { get; }
    public long Usn { get; }

    /// <summary>The same journal (volume and journal id) as <paramref name="other"/>.</summary>
    public bool IsSameJournal(ChangeJournalCheckpoint other) {
      return other != null && VolumeSerialNumber == other.VolumeSerialNumber && JournalId == other.JournalId;
    }

    public ChangeJournalCheckpoint WithUsn(long usn) {
      return new ChangeJournalCheckpoint(VolumeSerialNumber, JournalId, usn);
    }

    public bool Equals(ChangeJournalCheckpoint other) {
      return IsSameJournal(other) && Usn == other.Usn;
    }

    public override bool Equals(object obj) {
      return Equals(obj as ChangeJournalCheckpoint);
    }

    public override int GetHashCode() {
      return HashCode.Combine(VolumeSerialNumber, JournalId, Usn);
    }

    public override string ToString() {
      return $"volume {VolumeSerialNumber:x}, journal {JournalId:x}, USN {Usn:n0}";
    }
  }
}
