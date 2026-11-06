// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;

namespace VsChromium.Core.Files {
  /// <summary>
  /// The change journal of the volumes (NTFS/ReFS USN journal on Windows, see <see
  /// cref="Windows.WindowsChangeJournal"/>): the records of the changes of the files and
  /// directories since a position (<see cref="ChangeJournalCheckpoint"/>), used to list again only
  /// the directories that changed while no server was running (verification of the index cache).
  /// Null on the OSes without implementation (full scan).
  /// </summary>
  public interface IChangeJournal {
    /// <summary>
    /// The state of the change journal of the volume of <paramref name="directory"/> (an existing
    /// directory): never throws, the result says why the journal can't be used.
    /// </summary>
    ChangeJournalQueryResult Query(FullPath directory);

    /// <summary>
    /// Calls <paramref name="handler"/> for each record of the journal of the volume of
    /// <paramref name="directory"/> from the USN of <paramref name="start"/> (a record boundary:
    /// the next USN of a query) to at least <paramref name="endUsn"/> (records after it may be
    /// returned too). Throws <see cref="ChangeJournalException"/> if the journal can't be read
    /// (e.g. deleted, recreated, or the records after <paramref name="start"/> were overwritten).
    /// </summary>
    void ReadRecords(FullPath directory, ChangeJournalCheckpoint start, long endUsn, ChangeJournalRecordHandler handler,
                     CancellationToken cancellationToken);

    /// <summary>
    /// The time of the oldest record of the journal of the volume of <paramref name="directory"/>
    /// (diagnostics: how much history the journal keeps), or null if unknown.
    /// </summary>
    DateTime? GetOldestRecordTime(FullPath directory, ChangeJournalQueryResult state) => null;
  }

  /// <summary>The state of the change journal of a volume (see <see cref="IChangeJournal.Query"/>).</summary>
  public sealed class ChangeJournalQueryResult {
    private ChangeJournalQueryResult(ChangeJournalCheckpoint next, long firstUsn, string fileSystemName, string failureReason,
                                     long maximumSize = 0) {
      Next = next;
      FirstUsn = firstUsn;
      FileSystemName = fileSystemName;
      FailureReason = failureReason;
      MaximumSize = maximumSize;
    }

    public static ChangeJournalQueryResult Success(ChangeJournalCheckpoint next, long firstUsn, string fileSystemName,
                                                   long maximumSize = 0) {
      return new ChangeJournalQueryResult(next, firstUsn, fileSystemName, null, maximumSize);
    }

    /// <summary>The maximum size of the journal in bytes (0 if unknown): it keeps less history when it is small.</summary>
    public long MaximumSize { get; }

    public static ChangeJournalQueryResult Failure(string reason, string fileSystemName = null) {
      return new ChangeJournalQueryResult(null, 0, fileSystemName, reason);
    }

    public bool IsSuccess => FailureReason == null;

    /// <summary>
    /// The volume, the journal and its next USN (the end of the journal): a checkpoint read before
    /// a scan.
    /// </summary>
    public ChangeJournalCheckpoint Next { get; }

    /// <summary>The first USN still in the journal (older records were overwritten).</summary>
    public long FirstUsn { get; }

    /// <summary>The file system of the volume (e.g. "NTFS"), if known.</summary>
    public string FileSystemName { get; }

    /// <summary>Why the journal can't be used (e.g. "the change journal is not active on D:\"), or null.</summary>
    public string FailureReason { get; }

    /// <summary>
    /// Why the records from <paramref name="checkpoint"/> can't be read from this journal, or null
    /// if they can.
    /// </summary>
    public string CheckReadable(ChangeJournalCheckpoint checkpoint) {
      if (!IsSuccess)
        return FailureReason;
      if (checkpoint == null)
        return "no change journal checkpoint";
      if (checkpoint.VolumeSerialNumber != Next.VolumeSerialNumber)
        return "another volume";
      if (checkpoint.JournalId != Next.JournalId)
        return "the change journal was recreated";
      if (checkpoint.Usn < FirstUsn)
        return "the change journal wrapped (records after the checkpoint were overwritten)";
      if (checkpoint.Usn > Next.Usn)
        return "the checkpoint is after the end of the change journal";
      return null;
    }
  }

  /// <summary>A record of the change journal (see <see cref="IChangeJournal.ReadRecords"/>).</summary>
  public readonly struct ChangeJournalRecord {
    public ChangeJournalRecord(long usn, UInt128 fileId, UInt128 parentFileId, uint reason, uint fileAttributes, long timeStamp = 0) {
      Usn = usn;
      FileId = fileId;
      ParentFileId = parentFileId;
      Reason = reason;
      FileAttributes = fileAttributes;
      TimeStamp = timeStamp;
    }

    /// <summary>The time of the record (FILETIME, UTC), 0 if unknown (version 4 records).</summary>
    public long TimeStamp { get; }

    public long Usn { get; }
    /// <summary>The 128-bit id of the file or directory (see <see cref="DirectoryEntry.FileId"/>).</summary>
    public UInt128 FileId { get; }
    /// <summary>The 128-bit id of its parent directory when the record was written.</summary>
    public UInt128 ParentFileId { get; }
    /// <summary>The reasons of the record (USN_REASON_*, accumulated since the file was opened).</summary>
    public uint Reason { get; }
    /// <summary>The attributes of the file (0 if unknown: version 4 records).</summary>
    public uint FileAttributes { get; }

    public bool IsDirectory => (FileAttributes & (uint)System.IO.FileAttributes.Directory) != 0;

    public override string ToString() {
      return $"USN {Usn:n0}: id {FileId:x}, parent {ParentFileId:x}, reason 0x{Reason:x}, attributes 0x{FileAttributes:x}";
    }
  }

  public delegate void ChangeJournalRecordHandler(in ChangeJournalRecord record);

  /// <summary>An error reading the change journal (the message is the reason, for the logs and the dashboard).</summary>
  public sealed class ChangeJournalException : Exception {
    public ChangeJournalException(string message) : base(message) {
    }

    public ChangeJournalException(string message, Exception innerException) : base(message, innerException) {
    }
  }
}
