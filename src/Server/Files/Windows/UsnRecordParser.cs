// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers.Binary;

namespace VsChromium.Core.Files.Windows {
  /// <summary>
  /// Parses the records returned by <c>FSCTL_READ_USN_JOURNAL</c> /
  /// <c>FSCTL_READ_UNPRIVILEGED_USN_JOURNAL</c> (<c>USN_RECORD_V2</c>, <c>USN_RECORD_V3</c> and
  /// <c>USN_RECORD_V4</c>, little endian). Pure parsing (no Windows API): unit tested on synthetic
  /// buffers on all platforms.
  /// </summary>
  public static class UsnRecordParser {
    // USN_REASON_*
    public const uint DataOverwrite = 0x00000001;
    public const uint DataExtend = 0x00000002;
    public const uint DataTruncation = 0x00000004;
    public const uint FileCreate = 0x00000100;
    public const uint FileDelete = 0x00000200;
    public const uint SecurityChange = 0x00000800;
    public const uint RenameOldName = 0x00001000;
    public const uint RenameNewName = 0x00002000;
    public const uint BasicInfoChange = 0x00008000;
    public const uint HardLinkChange = 0x00010000;
    public const uint ReparsePointChange = 0x00100000;
    public const uint Close = 0x80000000;

    /// <summary>
    /// The size of the output buffer header of a read: the USN to read from next (8 bytes).
    /// </summary>
    public const int OutputHeaderSize = 8;

    /// <summary>
    /// Parses the output of a read of the journal (<paramref name="buffer"/>: the next USN, then
    /// the records), calls <paramref name="handler"/> for each record, and returns the next USN.
    /// Throws <see cref="ChangeJournalException"/> if the buffer is invalid.
    /// </summary>
    public static long Parse(ReadOnlySpan<byte> buffer, ChangeJournalRecordHandler handler) {
      if (buffer.Length < OutputHeaderSize)
        throw new ChangeJournalException("Invalid change journal read output: " + buffer.Length + " bytes");
      var nextUsn = BinaryPrimitives.ReadInt64LittleEndian(buffer);
      var offset = OutputHeaderSize;
      while (offset < buffer.Length) {
        var record = buffer.Slice(offset);
        if (record.Length < 8)
          throw new ChangeJournalException("Truncated change journal record header");
        var length = BinaryPrimitives.ReadInt32LittleEndian(record);
        if (length < 8 || length > record.Length)
          throw new ChangeJournalException("Invalid change journal record length: " + length);
        record = record.Slice(0, length);
        var majorVersion = BinaryPrimitives.ReadUInt16LittleEndian(record.Slice(4));
        handler(ParseRecord(record, majorVersion));
        // Note: Records are 8 byte aligned (the length includes the padding).
        offset += length;
      }
      return nextUsn;
    }

    private static ChangeJournalRecord ParseRecord(ReadOnlySpan<byte> record, ushort majorVersion) {
      switch (majorVersion) {
        case 2:
          // USN_RECORD_V2: 64-bit file references (FileName at 60).
          RequireLength(record, 60, majorVersion);
          return new ChangeJournalRecord(
            usn: BinaryPrimitives.ReadInt64LittleEndian(record.Slice(24)),
            fileId: BinaryPrimitives.ReadUInt64LittleEndian(record.Slice(8)),
            parentFileId: BinaryPrimitives.ReadUInt64LittleEndian(record.Slice(16)),
            reason: BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(40)),
            fileAttributes: BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(52)));
        case 3:
          // USN_RECORD_V3: FILE_ID_128 file references (FileName at 76).
          RequireLength(record, 76, majorVersion);
          return new ChangeJournalRecord(
            usn: BinaryPrimitives.ReadInt64LittleEndian(record.Slice(40)),
            fileId: ReadFileId128(record.Slice(8)),
            parentFileId: ReadFileId128(record.Slice(24)),
            reason: BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(56)),
            fileAttributes: BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(68)));
        case 4:
          // USN_RECORD_V4 (range tracking): FILE_ID_128 file references, no attributes (extents at 64).
          RequireLength(record, 64, majorVersion);
          return new ChangeJournalRecord(
            usn: BinaryPrimitives.ReadInt64LittleEndian(record.Slice(40)),
            fileId: ReadFileId128(record.Slice(8)),
            parentFileId: ReadFileId128(record.Slice(24)),
            reason: BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(48)),
            fileAttributes: 0);
        default:
          throw new ChangeJournalException("Unsupported change journal record version " + majorVersion);
      }
    }

    private static void RequireLength(ReadOnlySpan<byte> record, int length, ushort majorVersion) {
      if (record.Length < length)
        throw new ChangeJournalException($"Truncated change journal record (version {majorVersion}, {record.Length} bytes)");
    }

    /// <summary>A FILE_ID_128 (16 bytes, the first 8 bytes are the low part, as in FILE_ID_INFO).</summary>
    public static UInt128 ReadFileId128(ReadOnlySpan<byte> bytes) {
      return new UInt128(BinaryPrimitives.ReadUInt64LittleEndian(bytes.Slice(8)), BinaryPrimitives.ReadUInt64LittleEndian(bytes));
    }
  }
}
