// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestUsnRecordParser {
    private static readonly UInt128 FileId = new UInt128(0x1111_2222_3333_4444, 0x5555_6666_7777_8888);
    private static readonly UInt128 ParentId = new UInt128(0x9999_aaaa_bbbb_cccc, 0xdddd_eeee_ffff_0001);

    [TestMethod]
    public void ParsesVersion2Records() {
      var record = new byte[64];
      Header(record, 2);
      BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(8), 0x0001_0000_0000_1234);
      BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(16), 0x0002_0000_0000_0005);
      BinaryPrimitives.WriteInt64LittleEndian(record.AsSpan(24), 4096);
      BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(40), UsnRecordParser.FileCreate | UsnRecordParser.Close);
      BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(52), (uint)FileAttributes.Directory);
      var records = Parse(9000, record, out var nextUsn);
      Assert.AreEqual(9000L, nextUsn);
      var r = records.Single();
      Assert.AreEqual(4096L, r.Usn);
      Assert.AreEqual(new UInt128(0, 0x0001_0000_0000_1234), r.FileId);
      Assert.AreEqual(new UInt128(0, 0x0002_0000_0000_0005), r.ParentFileId);
      Assert.AreEqual(UsnRecordParser.FileCreate | UsnRecordParser.Close, r.Reason);
      Assert.IsTrue(r.IsDirectory);
    }

    [TestMethod]
    public void ParsesVersion3Records() {
      var record = new byte[80];
      Header(record, 3);
      WriteId(record, 8, FileId);
      WriteId(record, 24, ParentId);
      BinaryPrimitives.WriteInt64LittleEndian(record.AsSpan(40), 123_456);
      BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(56), UsnRecordParser.DataExtend);
      BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(68), (uint)FileAttributes.Archive);
      var r = Parse(200_000, record, out _).Single();
      Assert.AreEqual(123_456L, r.Usn);
      Assert.AreEqual(FileId, r.FileId);
      Assert.AreEqual(ParentId, r.ParentFileId);
      Assert.AreEqual(UsnRecordParser.DataExtend, r.Reason);
      Assert.IsFalse(r.IsDirectory);
    }

    [TestMethod]
    public void ParsesVersion4Records() {
      var record = new byte[80];
      Header(record, 4);
      WriteId(record, 8, FileId);
      WriteId(record, 24, ParentId);
      BinaryPrimitives.WriteInt64LittleEndian(record.AsSpan(40), 77);
      BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(48), UsnRecordParser.DataOverwrite);
      var r = Parse(100, record, out _).Single();
      Assert.AreEqual(77L, r.Usn);
      Assert.AreEqual(FileId, r.FileId);
      Assert.AreEqual(ParentId, r.ParentFileId);
      Assert.AreEqual(UsnRecordParser.DataOverwrite, r.Reason);
      Assert.AreEqual(0u, r.FileAttributes);
    }

    [TestMethod]
    public void ParsesSeveralRecordsOfDifferentVersions() {
      var v2 = new byte[64];
      Header(v2, 2);
      BinaryPrimitives.WriteInt64LittleEndian(v2.AsSpan(24), 1);
      var v3 = new byte[80];
      Header(v3, 3);
      BinaryPrimitives.WriteInt64LittleEndian(v3.AsSpan(40), 2);
      var records = Parse(3, v2.Concat(v3).Concat(v2).ToArray(), out _);
      CollectionAssert.AreEqual(new long[] { 1, 2, 1 }, records.Select(x => x.Usn).ToArray());
    }

    [TestMethod]
    public void EmptyOutputHasNoRecords() {
      Assert.AreEqual(0, Parse(55, Array.Empty<byte>(), out var nextUsn).Count);
      Assert.AreEqual(55L, nextUsn);
    }

    [TestMethod]
    public void InvalidBuffersThrow() {
      Assert.Throws<ChangeJournalException>(() => UsnRecordParser.Parse(new byte[4], (in ChangeJournalRecord r) => { }));
      var record = new byte[64];
      Header(record, 2);
      // Length larger than the buffer.
      BinaryPrimitives.WriteInt32LittleEndian(record, 128);
      Assert.Throws<ChangeJournalException>(() => Parse(0, record, out _));
      // Unknown version.
      Header(record, 9);
      Assert.Throws<ChangeJournalException>(() => Parse(0, record, out _));
      // Truncated record.
      var truncated = new byte[40];
      Header(truncated, 3);
      Assert.Throws<ChangeJournalException>(() => Parse(0, truncated, out _));
    }

    private static void Header(byte[] record, ushort majorVersion) {
      BinaryPrimitives.WriteInt32LittleEndian(record, record.Length);
      BinaryPrimitives.WriteUInt16LittleEndian(record.AsSpan(4), majorVersion);
    }

    private static void WriteId(byte[] record, int offset, UInt128 id) {
      BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(offset), (ulong)id);
      BinaryPrimitives.WriteUInt64LittleEndian(record.AsSpan(offset + 8), (ulong)(id >> 64));
    }

    private static List<ChangeJournalRecord> Parse(long nextUsn, byte[] records, out long parsedNextUsn) {
      var buffer = new byte[8 + records.Length];
      BinaryPrimitives.WriteInt64LittleEndian(buffer, nextUsn);
      records.CopyTo(buffer, 8);
      var result = new List<ChangeJournalRecord>();
      parsedNextUsn = UsnRecordParser.Parse(buffer, (in ChangeJournalRecord r) => result.Add(r));
      return result;
    }
  }

  /// <summary>
  /// <see cref="WindowsChangeJournal"/> on the volume of the temporary directory (inconclusive if
  /// its change journal is not active).
  /// </summary>
  [TestClass]
  [SupportedOSPlatform("windows")]
  public class TestWindowsChangeJournal {
    private string _root;

    [TestInitialize]
    public void Initialize() {
      if (!OperatingSystem.IsWindows())
        Assert.Inconclusive("Windows only");
      _root = Path.Combine(Path.GetTempPath(), "VsChromium.Tests", "ChangeJournal-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() {
      try {
        if (_root != null)
          Directory.Delete(_root, recursive: true);
      }
      catch (IOException) {
      }
    }

    private ChangeJournalQueryResult QueryOrInconclusive(IChangeJournal journal) {
      var result = journal.Query(new FullPath(_root));
      if (!result.IsSuccess)
        Assert.Inconclusive("Change journal not usable on the temporary directory: " + result.FailureReason);
      return result;
    }

    [TestMethod]
    public void RecordsOfChangesHaveTheIdsOfTheDirectories() {
      var journal = new WindowsChangeJournal();
      var start = QueryOrInconclusive(journal);
      Assert.IsTrue(start.FileSystemName == "NTFS" || start.FileSystemName == "ReFS", start.FileSystemName);
      Assert.IsNull(start.CheckReadable(start.Next));

      var rootId = WindowsFileSystem.GetFileId(_root);
      var subdirectory = Path.Combine(_root, "sub");
      Directory.CreateDirectory(subdirectory);
      var subdirectoryId = WindowsFileSystem.GetFileId(subdirectory);
      File.WriteAllText(Path.Combine(subdirectory, "file.txt"), "contents");
      var fileId = WindowsFileSystem.GetFileId(Path.Combine(subdirectory, "file.txt"));

      var end = journal.Query(new FullPath(_root));
      Assert.IsTrue(end.Next.Usn > start.Next.Usn);
      var records = new List<ChangeJournalRecord>();
      journal.ReadRecords(new FullPath(_root), start.Next, end.Next.Usn, (in ChangeJournalRecord r) => records.Add(r), CancellationToken.None);
      Assert.IsTrue(records.Any(x => x.FileId == subdirectoryId && x.ParentFileId == rootId && x.IsDirectory &&
                                     (x.Reason & UsnRecordParser.FileCreate) != 0), "creation of the directory");
      Assert.IsTrue(records.Any(x => x.FileId == fileId && x.ParentFileId == subdirectoryId && !x.IsDirectory), "file of the directory");
      Assert.IsTrue(records.All(x => x.Usn >= start.Next.Usn));
    }

    [TestMethod]
    public void ReadingFromAnotherJournalFails() {
      var journal = new WindowsChangeJournal();
      var start = QueryOrInconclusive(journal);
      var other = new ChangeJournalCheckpoint(start.Next.VolumeSerialNumber, start.Next.JournalId + 1, start.Next.Usn);
      Assert.AreEqual("the change journal was recreated", start.CheckReadable(other));
      Assert.Throws<ChangeJournalException>(() =>
        journal.ReadRecords(new FullPath(_root), other, start.Next.Usn + 1, (in ChangeJournalRecord r) => { }, CancellationToken.None));
    }

    [TestMethod]
    public void CheckpointBeforeTheFirstUsnIsNotReadable() {
      var start = QueryOrInconclusive(new WindowsChangeJournal());
      var wrapped = start.Next.WithUsn(start.FirstUsn - 8);
      StringAssert.Contains(start.CheckReadable(wrapped), "wrapped");
      StringAssert.Contains(start.CheckReadable(start.Next.WithUsn(start.Next.Usn + 8)), "after the end");
    }

    [TestMethod]
    public void MissingDirectoryFails() {
      var result = new WindowsChangeJournal().Query(new FullPath(Path.Combine(_root, "missing")));
      Assert.IsFalse(result.IsSuccess);
    }
  }
}
