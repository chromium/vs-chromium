// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Configuration;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.ProgressTracking;
using VsChromium.Server.Projects;
using VsChromium.Server.Projects.ProjectFile;
using VsChromium.Server.Threads;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// <see cref="ChangeJournalDirectoryMapper"/>: the mapping of the records of the change journal to
  /// the directories to list again.
  /// </summary>
  [TestClass]
  public class TestChangeJournalDirectoryMapper {
    private const uint Directory = (uint)FileAttributes.Directory;
    private const uint File = (uint)FileAttributes.Archive;
    private readonly FileSystemNameFactory _factory = new FileSystemNameFactory();

    // root (1)
    //   a (2)
    //     b (3)
    //   link (4, symbolic link to the same volume: the id of its target)
    //     inner (5)
    //   otherlink (0, symbolic link to another volume)
    //     otherinner (6, other volume)
    //   noid (0)
    private DirectorySnapshot _root, _a, _b, _link, _inner, _otherLink, _noId;

    [TestInitialize]
    public void Initialize() {
      var rootName = _factory.CreateAbsoluteDirectoryName(new FullPath(Path.Combine(Path.GetTempPath(), "root")));
      var aName = _factory.CreateDirectoryName(rootName, "a");
      var bName = _factory.CreateDirectoryName(aName, "b");
      var linkName = _factory.CreateDirectoryName(rootName, "link", true);
      var innerName = _factory.CreateDirectoryName(linkName, "inner");
      var noIdName = _factory.CreateDirectoryName(rootName, "noid");
      var otherLinkName = _factory.CreateDirectoryName(rootName, "otherlink", true);
      var otherInnerName = _factory.CreateDirectoryName(otherLinkName, "otherinner");
      _b = Dir(bName, 3, false);
      _a = Dir(aName, 2, false, _b);
      _inner = Dir(innerName, 5, false);
      _link = Dir(linkName, 4, true, _inner);
      _otherLink = Dir(otherLinkName, 0, true, Dir(otherInnerName, 6, false));
      _noId = Dir(noIdName, 0, false);
      _root = Dir(rootName, 1, false, _a, _link, _otherLink, _noId);
    }

    private static DirectorySnapshot Dir(DirectoryName name, ulong id, bool isSymLink, params DirectorySnapshot[] children) {
      return new DirectorySnapshot(new DirectoryData(name, isSymLink, id), children, Array.Empty<FileName>(), Array.Empty<FileMetadata>());
    }

    private static ChangeJournalRecord Record(ulong id, ulong parent, uint reason, uint attributes) {
      return new ChangeJournalRecord(100, id, parent, reason, attributes);
    }

    private ChangeJournalDirectoryMapper Map(params ChangeJournalRecord[] records) {
      var mapper = new ChangeJournalDirectoryMapper(_root);
      foreach (var record in records) {
        mapper.Add(record);
      }
      return mapper;
    }

    [TestMethod]
    public void DirectoriesOfSymbolicLinkSubtreesAndWithoutIdAreNotMapped() {
      var mapper = Map();
      Assert.AreEqual(5, mapper.DirectoryCount, "root, a, b, link, inner (not otherinner: other volume)");
      Assert.AreEqual(1, mapper.ZeroIdDirectoryCount);
      Assert.AreEqual(0, mapper.ChangedDirectories.Count);
      CollectionAssert.AreEqual(new[] { _link }, mapper.SymLinks.ToList());
    }

    [TestMethod]
    public void FileRecordsMarkTheirParent() {
      var mapper = Map(Record(1000, 3, UsnRecordParser.DataExtend | UsnRecordParser.Close, File),
                       Record(1001, 2, UsnRecordParser.FileCreate, File));
      CollectionAssert.AreEquivalent(new[] { _b, _a }, mapper.ChangedDirectories.ToList());
      Assert.AreEqual(2L, mapper.RecordCount);
    }

    [TestMethod]
    public void FileRecordsWithAnyReasonMarkTheirParent() {
      // E.g. the last write time set explicitly.
      var mapper = Map(Record(1000, 3, UsnRecordParser.BasicInfoChange, File));
      CollectionAssert.AreEquivalent(new[] { _b }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void RecordsOfUnknownParentsAreIgnored() {
      // Outside of the project, in an ignored directory, or in a symbolic link subtree (other volume).
      var mapper = Map(Record(1000, 999, UsnRecordParser.FileCreate, File), Record(1001, 6, UsnRecordParser.FileCreate, File));
      Assert.AreEqual(0, mapper.ChangedDirectories.Count);
    }

    [TestMethod]
    public void NewDirectoriesMarkTheirParent() {
      var mapper = Map(Record(1000, 2, UsnRecordParser.FileCreate, Directory));
      CollectionAssert.AreEquivalent(new[] { _a }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void DeletedDirectoriesMarkTheirParent() {
      var mapper = Map(Record(3, 2, UsnRecordParser.FileDelete | UsnRecordParser.Close, Directory));
      CollectionAssert.AreEquivalent(new[] { _a }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void DirectoriesMovedOutMarkTheirParentInTheTree() {
      // The record of the new name has the new parent (outside of the project).
      var mapper = Map(Record(3, 999, UsnRecordParser.RenameNewName, Directory));
      CollectionAssert.AreEquivalent(new[] { _a }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void DirectoriesMovedInMarkTheirNewParent() {
      var mapper = Map(Record(777, 888, UsnRecordParser.RenameOldName, Directory), Record(777, 3, UsnRecordParser.RenameNewName, Directory));
      CollectionAssert.AreEquivalent(new[] { _b }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void DirectoriesRenamedMarkTheirParent() {
      var mapper = Map(Record(3, 2, UsnRecordParser.RenameOldName, Directory), Record(3, 1, UsnRecordParser.RenameNewName, Directory));
      CollectionAssert.AreEquivalent(new[] { _a, _root }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void OtherChangesOfDirectoriesAreIgnored() {
      // The time, the attributes or the security of a directory: not used.
      var mapper = Map(Record(3, 2, UsnRecordParser.BasicInfoChange | UsnRecordParser.SecurityChange | UsnRecordParser.Close, Directory));
      Assert.AreEqual(0, mapper.ChangedDirectories.Count);
    }

    [TestMethod]
    public void ReplacedSymbolicLinksMarkTheirParent() {
      var mapper = Map(Record(4, 1, UsnRecordParser.ReparsePointChange, Directory));
      CollectionAssert.AreEquivalent(new[] { _root }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void RecordsInTheTargetOfASymbolicLinkMarkTheLink() {
      // A file created in the target of "link" (id 4) and in "inner".
      var mapper = Map(Record(1000, 4, UsnRecordParser.FileCreate, File), Record(1001, 5, UsnRecordParser.FileCreate, File));
      CollectionAssert.AreEquivalent(new[] { _link, _inner }, mapper.ChangedDirectories.ToList());
      Assert.AreEqual(0, mapper.SymLinksToScan.Count);
    }

    [TestMethod]
    public void TargetOfASymbolicLinkRenamedMarksTheLinkToScan() {
      // The target (id 4) moved out of its parent (unknown: outside of the project).
      var mapper = Map(Record(4, 999, UsnRecordParser.RenameOldName, Directory));
      CollectionAssert.AreEquivalent(new[] { _link }, mapper.SymLinksToScan.ToList());
      CollectionAssert.AreEquivalent(new[] { _root }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void TargetsInsideTheProjectMarkAllTheirDirectories() {
      // "alias" is a link to "a": both have the id 2.
      var rootName = _root.DirectoryName;
      var alias = Dir(_factory.CreateDirectoryName(rootName, "alias", true), 2, true,
        Dir(_factory.CreateDirectoryName(_factory.CreateDirectoryName(rootName, "alias", true), "b"), 3, false));
      var root = new DirectorySnapshot(_root.DirectoryData, _root.ChildDirectories.Append(alias).ToList(),
        Array.Empty<FileName>(), Array.Empty<FileMetadata>());
      var mapper = new ChangeJournalDirectoryMapper(root);
      mapper.Add(Record(1000, 3, UsnRecordParser.FileCreate, File));
      CollectionAssert.AreEquivalent(new[] { _b, alias.ChildDirectories[0] }, mapper.ChangedDirectories.ToList());
    }

    [TestMethod]
    public void VersionFourRecordsAreFileRecords() {
      // No attributes: data changes of files (range tracking).
      var mapper = Map(Record(1000, 2, UsnRecordParser.DataOverwrite, 0));
      CollectionAssert.AreEquivalent(new[] { _a }, mapper.ChangedDirectories.ToList());
    }
  }

  /// <summary>
  /// The verification of a tree from the change journal (catch-up, <see
  /// cref="FileSystemSnapshotBuilder"/>) on a real directory of the volume of the temporary
  /// directory: the tree is the same as a full scan, and only the changed directories are listed
  /// again; and the fallbacks to the full scan. Inconclusive if the change journal of the volume
  /// is not active.
  /// </summary>
  [TestClass]
  [SupportedOSPlatform("windows")]
  public class TestChangeJournalCatchUp {
    private static readonly DateTime OldTime = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private string _root;
    private string _outside;
    private WindowsFileSystem _fileSystem;
    private IndexingWorkerPool _workerPool;
    private IProject _project;

    [TestInitialize]
    public void Initialize() {
      if (!OperatingSystem.IsWindows())
        Assert.Inconclusive("Windows only");
      var parent = Path.Combine(Path.GetTempPath(), "VsChromium.Tests", "CatchUp-" + Guid.NewGuid().ToString("N"));
      _root = Path.Combine(parent, "project");
      _outside = Path.Combine(parent, "outside");
      System.IO.Directory.CreateDirectory(_root);
      System.IO.Directory.CreateDirectory(_outside);
      _fileSystem = new WindowsFileSystem();
      _workerPool = new IndexingWorkerPool();
      var query = new WindowsChangeJournal().Query(new FullPath(_root));
      if (!query.IsSuccess)
        Assert.Inconclusive("Change journal not usable on the temporary directory: " + query.FailureReason);

      WriteFile(ConfigurationFileNames.ProjectFileName, "[SourceExplorer.ignore]\nignored/\n");
      foreach (var directory in new[] { "a", Path.Combine("a", "b"), "c", "d", Path.Combine("d", "e"), "ignored", "tools" }) {
        System.IO.Directory.CreateDirectory(Path.Combine(_root, directory));
        for (var i = 0; i < 3; i++) {
          WriteFile(Path.Combine(directory, $"file{i}.txt"), $"contents of {directory} {i}");
        }
      }
      _project = new ProjectFileDiscoveryProvider(_fileSystem).GetProjectFromAnyPath(new FullPath(_root));
    }

    [TestCleanup]
    public void Cleanup() {
      _workerPool?.Dispose();
      try {
        if (_root != null)
          System.IO.Directory.Delete(Path.GetDirectoryName(_root), recursive: true);
      }
      catch (IOException) {
      }
    }

    private void WriteFile(string relativePath, string text, DateTime? lastWriteTimeUtc = null) {
      var path = Path.Combine(_root, relativePath);
      System.IO.File.WriteAllText(path, text);
      System.IO.File.SetLastWriteTimeUtc(path, lastWriteTimeUtc ?? OldTime);
    }

    private FileSystemSnapshot FullScan(IFileSystem fileSystem = null, IChangeJournal journal = null) {
      var builder = new FileSystemSnapshotBuilder(fileSystem ?? _fileSystem, new NullProgressTrackerFactory(), _workerPool,
        journal ?? new WindowsChangeJournal());
      return builder.Compute(new FileSystemNameFactory(), null, null, new[] { _project }, 1, CancellationToken.None);
    }

    private (FileSystemSnapshot Snapshot, ProjectVerification Verification) Verify(FileSystemSnapshot old,
      ChangeJournalCheckpoint checkpoint, IChangeJournal journal = null, bool useNullJournal = false) {
      var builder = new FileSystemSnapshotBuilder(_fileSystem, new NullProgressTrackerFactory(), _workerPool,
        useNullJournal ? null : journal ?? new WindowsChangeJournal());
      var root = new FullPath(_root);
      var options = new SnapshotComputeOptions {
        FullScanRoots = new HashSet<FullPath> { root },
        VerifyRoots = new Dictionary<FullPath, ChangeJournalCheckpoint> { [root] = checkpoint },
      };
      var snapshot = builder.Compute(new FileSystemNameFactory(), old,
        new FullPathChanges(Array.Empty<PathChangeEntry>()), new[] { _project }, 2, CancellationToken.None, options);
      return (snapshot, options.Verifications[root]);
    }

    private static ProjectRootSnapshot Root(FileSystemSnapshot snapshot) => snapshot.ProjectRoots.Single();

    [TestMethod]
    public void NoChangeReusesTheTree() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;
      Assert.IsNotNull(checkpoint);
      var (snapshot, verification) = Verify(old, checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      Assert.AreEqual(0, verification.ListedDirectoryCount, verification.ToString());
      Assert.AreSame(Root(old).Directory, Root(snapshot).Directory, "Nothing changed: the same tree");
      Assert.IsTrue(Root(snapshot).Checkpoint.Usn >= checkpoint.Usn);
    }

    [TestMethod]
    public void ChangesAreTheSameAsAFullScan() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;

      // Edit with the same size and another time, create and delete files.
      WriteFile(Path.Combine("a", "file0.txt"), "CONTENTS of a 0", OldTime.AddMinutes(1));
      WriteFile(Path.Combine("c", "new.txt"), "new file");
      System.IO.File.Delete(Path.Combine(_root, "c", "file1.txt"));
      // Create, delete and rename directories.
      System.IO.Directory.CreateDirectory(Path.Combine(_root, "new", "sub"));
      WriteFile(Path.Combine("new", "sub", "file.txt"), "in a new directory");
      System.IO.Directory.Delete(Path.Combine(_root, "a", "b"), recursive: true);
      System.IO.Directory.Move(Path.Combine(_root, "c"), Path.Combine(_root, "c_renamed"));
      // Move a subtree out of the project, and another one in.
      System.IO.Directory.Move(Path.Combine(_root, "d", "e"), Path.Combine(_outside, "e"));
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "moved", "inner"));
      System.IO.File.WriteAllText(Path.Combine(_outside, "moved", "inner", "file.txt"), "moved in");
      System.IO.Directory.Move(Path.Combine(_outside, "moved"), Path.Combine(_root, "d", "moved"));
      // Ignored directory: not in the tree.
      WriteFile(Path.Combine("ignored", "new.txt"), "ignored");

      var (snapshot, verification) = Verify(old, checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
      // a (edit, b deleted), c_renamed (as a new directory: scanned), root (new, c renamed), d (e
      // moved out, moved in): the other directories are not listed again.
      Assert.IsTrue(verification.ListedDirectoryCount <= 4, verification.ToString());
      Assert.IsTrue(verification.ScannedSubtreeCount >= 3, "new, c_renamed, d/moved: " + verification);
      Assert.IsTrue(verification.RecordCount > 0);
    }

    [TestMethod]
    public void TargetsOfFileSymbolicLinksAreCheckedAgain() {
      try {
        System.IO.File.CreateSymbolicLink(Path.Combine(_root, "a", "link.txt"), Path.Combine("..", "tools", "file0.txt"));
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }
      var old = FullScan();
      var a = Root(old).Directory.ChildDirectories.Single(x => x.DirectoryName.Name == "a");
      var link = a.ChildFiles.ToList().FindIndex(x => x.Name == "link.txt");
      Assert.IsTrue(a.IsSymLinkFile(link));

      // A change of the target is not a change of the directory of the link.
      WriteFile(Path.Combine("tools", "file0.txt"), "new contents of the target, longer", OldTime.AddMinutes(5));
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      Assert.AreEqual(1, verification.SymLinkFileCount);
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
      var newA = Root(snapshot).Directory.ChildDirectories.Single(x => x.DirectoryName.Name == "a");
      Assert.AreEqual(new FileMetadata(OldTime.AddMinutes(5), "new contents of the target, longer".Length), newA.ChildFileMetadata[link]);
    }

    [TestMethod]
    public void DirectorySymbolicLinksToTheSameVolumeAreCaughtUpFromTheJournal() {
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "target", "inner"));
      System.IO.File.WriteAllText(Path.Combine(_outside, "target", "inner", "file.txt"), "in the target");
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "target"));
      // A link to a directory of the project too: its target is in the tree twice.
      CreateDirectoryLink("dirlink_a", Path.Combine(_root, "a"));
      var old = FullScan();
      var link = Root(old).Directory.ChildDirectories.Single(x => x.DirectoryName.Name == "dirlink");
      Assert.AreEqual(WindowsFileSystem.GetFileId(Path.Combine(_outside, "target")), link.FileId, "The id of the target");

      // Changes through the links (the records have the ids of the targets).
      System.IO.File.WriteAllText(Path.Combine(_outside, "target", "inner", "new.txt"), "new");
      WriteFile(Path.Combine("a", "new.txt"), "new in a");
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      Assert.AreEqual(0, verification.SymLinkSubtreeCount, "Not scanned: " + verification);
      Assert.AreEqual(2, verification.CheckedSymLinkCount, verification.ToString());
      Assert.AreEqual(3, verification.ListedDirectoryCount, "target/inner, a and dirlink_a: " + verification);
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
    }

    [TestMethod]
    public void RetargetedDirectorySymbolicLinksAreScannedEntirely() {
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "target1"));
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "target2"));
      System.IO.File.WriteAllText(Path.Combine(_outside, "target2", "file.txt"), "in target 2");
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "target1"));
      var old = FullScan();
      System.IO.Directory.Delete(Path.Combine(_root, "dirlink"));
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "target2"));
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
    }

    [TestMethod]
    public void DirectorySymbolicLinksWhoseTargetIsRenamedOutsideOfTheProjectAreScannedEntirely() {
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "parent", "target"));
      System.IO.File.WriteAllText(Path.Combine(_outside, "parent", "target", "file.txt"), "in the target");
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "other", "target"));
      System.IO.File.WriteAllText(Path.Combine(_outside, "other", "target", "other.txt"), "in another target");
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "parent", "target"));
      var old = FullScan();
      // The link resolves to another directory now (no record in the tree).
      System.IO.Directory.Move(Path.Combine(_outside, "parent"), Path.Combine(_outside, "parent_renamed"));
      System.IO.Directory.Move(Path.Combine(_outside, "other"), Path.Combine(_outside, "parent"));
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method, verification.ToString());
      Assert.AreEqual(1, verification.SymLinkSubtreeCount, verification.ToString());
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
      Assert.IsTrue(Root(snapshot).Directory.ChildDirectories.Single(x => x.DirectoryName.Name == "dirlink").ChildFiles
        .Any(x => x.Name == "other.txt"));
    }

    [TestMethod]
    public void SelfCheckFindsRetargetedDirectorySymbolicLinks() {
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "target1"));
      System.IO.Directory.CreateDirectory(Path.Combine(_outside, "target2"));
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "target1"));
      var old = FullScan();
      System.IO.Directory.Delete(Path.Combine(_root, "dirlink"));
      CreateDirectoryLink("dirlink", Path.Combine(_outside, "target2"));
      var (checkpoint, _, reported) = SelfCheck(Root(old));
      Assert.AreEqual(Root(old).Checkpoint, checkpoint, "Kept");
      Assert.IsTrue(reported.Any(x => x.RelativePath.Value == "dirlink" && x.ChangeKind == PathChangeKind.Created),
        string.Join(", ", reported));
    }

    private void CreateDirectoryLink(string name, string target) {
      try {
        System.IO.Directory.CreateSymbolicLink(Path.Combine(_root, name), target);
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }
    }

    [TestMethod]
    public void WrappedJournalFallsBackToAFullScan() {
      var old = FullScan();
      var query = new WindowsChangeJournal().Query(new FullPath(_root));
      WriteFile(Path.Combine("a", "new.txt"), "new");
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint.WithUsn(query.FirstUsn - 8));
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      StringAssert.Contains(verification.FallbackReason, "wrapped");
      // Diagnostics: the journal is too small.
      Assert.AreEqual(query.FirstUsn - 8, verification.CheckpointUsn);
      Assert.IsTrue(verification.JournalMaximumSize > 0);
      Assert.IsNotNull(verification.OldestRecordTimeUtc);
      StringAssert.Contains(verification.Hint, "fsutil usn createjournal");
      StringAssert.Contains(verification.ToString(), "fsutil usn createjournal");
      AssertSameTree(Root(FullScan()).Directory, Root(snapshot).Directory);
      // The checkpoint of the full scan.
      Assert.IsTrue(Root(snapshot).Checkpoint.Usn >= query.Next.Usn);
    }

    [TestMethod]
    public void OtherJournalFallsBackToAFullScan() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;
      var (_, verification) = Verify(old, new ChangeJournalCheckpoint(checkpoint.VolumeSerialNumber, checkpoint.JournalId + 1, checkpoint.Usn,
        checkpoint.TimeUtc));
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("the change journal was recreated", verification.FallbackReason);
      Assert.AreEqual(checkpoint.Usn, verification.CheckpointUsn);
      Assert.AreEqual(checkpoint.TimeUtc, verification.CheckpointTimeUtc);
      StringAssert.Contains(verification.Hint, "created again");
    }

    [TestMethod]
    public void NoCheckpointFallsBackToAFullScan() {
      var old = FullScan();
      var (_, verification) = Verify(old, null);
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("the index cache has no change journal checkpoint", verification.FallbackReason);
    }

    [TestMethod]
    public void NoJournalFallsBackToAFullScan() {
      var old = FullScan();
      var (_, verification) = Verify(old, Root(old).Checkpoint, useNullJournal: true);
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("no change journal on this platform", verification.FallbackReason);
    }

    [TestMethod]
    public void TreeWithoutIdsFallsBackToAFullScan() {
      // E.g. a tree scanned by PortableFileSystem.
      var old = FullScan(new PortableFileSystem());
      Assert.AreEqual(UInt128.Zero, Root(old).Directory.FileId);
      var (_, verification) = Verify(old, Root(old).Checkpoint);
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("the index cache has no directory ids", verification.FallbackReason);
    }

    [TestMethod]
    public void JournalReadErrorFallsBackToAFullScan() {
      var old = FullScan();
      var (_, verification) = Verify(old, Root(old).Checkpoint, new FailingReadJournal(new WindowsChangeJournal()));
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("read error", verification.FallbackReason);
    }

    [TestMethod]
    public void InactiveJournalFallsBackToAFullScan() {
      var old = FullScan();
      var (snapshot, verification) = Verify(old, Root(old).Checkpoint, new InactiveJournal());
      Assert.AreEqual(VerificationMethod.FullScan, verification.Method);
      Assert.AreEqual("the change journal is not active", verification.FallbackReason);
      Assert.IsNull(Root(snapshot).Checkpoint, "No checkpoint without journal");
    }

    // Journal self-check of the saves of the index cache (ChangeJournalSelfCheck)

    private (ChangeJournalCheckpoint Checkpoint, ChangeJournalSelfCheckResult Result, List<PathChangeEntry> Reported) SelfCheck(
      ProjectRootSnapshot root, Func<FileName, FileMetadata, FileMetadata> savedMetadata = null, IChangeJournal journal = null) {
      var reported = new List<PathChangeEntry>();
      var check = new ChangeJournalSelfCheck(journal ?? new WindowsChangeJournal(), _fileSystem, changes => reported.AddRange(changes));
      var checkpoint = check.Run(root, savedMetadata ?? ((file, metadata) => metadata), CancellationToken.None);
      return (checkpoint, check.LastResult, reported);
    }

    private FileSystemSnapshot ApplyChanges(FileSystemSnapshot old, params PathChangeEntry[] changes) {
      var builder = new FileSystemSnapshotBuilder(_fileSystem, new NullProgressTrackerFactory(), _workerPool, new WindowsChangeJournal());
      return builder.Compute(new FileSystemNameFactory(), old, new FullPathChanges(changes), new[] { _project }, 3, CancellationToken.None);
    }

    private PathChangeEntry Change(string relativePath, PathChangeKind kind, PathKind pathKind = PathKind.File) {
      return new PathChangeEntry(new FullPath(_root), new RelativePath(relativePath), kind, pathKind);
    }

    [TestMethod]
    public void SelfCheckAdvancesTheCheckpointWhenTheTreeIsUpToDate() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;
      WriteFile(Path.Combine("a", "new.txt"), "new");
      WriteFile(Path.Combine("ignored", "new.txt"), "ignored");
      // The change is applied (as by the directory watcher).
      var updated = ApplyChanges(old, Change(Path.Combine("a", "new.txt"), PathChangeKind.Created));
      Assert.AreSame(Root(old).CheckpointHolder, Root(updated).CheckpointHolder, "Incremental updates share the checkpoint");

      var (newCheckpoint, result, reported) = SelfCheck(Root(updated));
      Assert.AreEqual(0, reported.Count, string.Join(", ", reported));
      Assert.IsNull(result.FailureReason);
      Assert.IsTrue(newCheckpoint.Usn > checkpoint.Usn);
      Assert.AreEqual(1, result.ListedDirectoryCount, "a");
      Assert.AreEqual(newCheckpoint, Root(updated).Checkpoint, "The checkpoint of the tree advanced");

      // The next catch-up starts from there: nothing to list again.
      var (_, verification) = Verify(updated, newCheckpoint);
      Assert.AreEqual(VerificationMethod.ChangeJournal, verification.Method);
      Assert.AreEqual(0, verification.ListedDirectoryCount, verification.ToString());
    }

    [TestMethod]
    public void SelfCheckReportsMissedChangesAndKeepsTheCheckpoint() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;
      // Changes not applied to the tree (missed by the directory watcher).
      WriteFile(Path.Combine("a", "new.txt"), "new");
      System.IO.File.Delete(Path.Combine(_root, "d", "file1.txt"));
      WriteFile(Path.Combine("d", "file2.txt"), "other size than before", OldTime.AddMinutes(1));
      System.IO.Directory.CreateDirectory(Path.Combine(_root, "newdir"));
      System.IO.Directory.Delete(Path.Combine(_root, "c"), recursive: true);
      System.IO.Directory.CreateDirectory(Path.Combine(_root, "c"));

      var (newCheckpoint, result, reported) = SelfCheck(Root(old));
      Assert.AreEqual(checkpoint, newCheckpoint, "The checkpoint is kept");
      Assert.AreEqual(checkpoint, Root(old).Checkpoint);
      var descriptions = reported.Select(x => $"{x.ChangeKind} {x.PathKind} {x.RelativePath.Value}").OrderBy(x => x).ToList();
      // Note: The files of the deleted directory "c" can be reported deleted too (their records
      // mark the deleted directory, listed again by its path): harmless.
      var withoutFilesOfC = descriptions.Where(x => !x.StartsWith("Deleted File c" + Path.DirectorySeparatorChar)).ToList();
      CollectionAssert.AreEqual(new[] {
        $"Changed File {Path.Combine("d", "file2.txt")}",
        "Created Directory c",
        "Created Directory newdir",
        $"Created File {Path.Combine("a", "new.txt")}",
        "Deleted Directory c",
        $"Deleted File {Path.Combine("d", "file1.txt")}",
      }, withoutFilesOfC, string.Join(", ", descriptions));
      Assert.AreEqual(result.Changes.Count, reported.Count);
    }

    [TestMethod]
    public void SelfCheckComparesTheSavedMetadataOfTheFiles() {
      var old = FullScan();
      var path = Path.Combine("a", "file1.txt");
      WriteFile(path, "edited contents, longer than before", OldTime.AddMinutes(2));
      var edited = new FileMetadata(OldTime.AddMinutes(2), "edited contents, longer than before".Length);
      // The edit was not read: a difference.
      Assert.AreEqual(1, SelfCheck(Root(old)).Reported.Count);
      // The edit was read (the cache saves the metadata of the read): no difference.
      var (checkpoint, _, reported) = SelfCheck(Root(old), (file, metadata) => file.Name == "file1.txt" && file.Parent.Name == "a" ? edited : metadata);
      Assert.AreEqual(0, reported.Count, string.Join(", ", reported));
      Assert.AreEqual(checkpoint, Root(old).Checkpoint);
    }

    [TestMethod]
    public void SelfCheckKeepsTheCheckpointIfTheJournalCantBeRead() {
      var old = FullScan();
      var checkpoint = Root(old).Checkpoint;
      WriteFile(Path.Combine("a", "new.txt"), "new");
      var (newCheckpoint, result, reported) = SelfCheck(Root(old), journal: new InactiveJournal());
      Assert.AreEqual(checkpoint, newCheckpoint);
      Assert.AreEqual("the change journal is not active", result.FailureReason);
      Assert.AreEqual(0, reported.Count);
      (newCheckpoint, result, _) = SelfCheck(Root(old), journal: new FailingReadJournal(new WindowsChangeJournal()));
      Assert.AreEqual(checkpoint, newCheckpoint);
      Assert.AreEqual("read error", result.FailureReason);
    }

    /// <summary>The same tree: names, metadata, symbolic links, directory ids.</summary>
    private static void AssertSameTree(DirectorySnapshot expected, DirectorySnapshot actual) {
      var path = expected.DirectoryName.RelativePath.Value ?? "<root>";
      Assert.AreEqual(expected.DirectoryName.Name, actual.DirectoryName.Name, path);
      Assert.AreEqual(expected.IsSymLink, actual.IsSymLink, path);
      Assert.AreEqual(expected.FileId, actual.FileId, path);
      CollectionAssert.AreEqual(expected.ChildFiles.Select(x => x.Name).ToList(), actual.ChildFiles.Select(x => x.Name).ToList(), path);
      CollectionAssert.AreEqual(expected.ChildFileMetadata.ToArray(), actual.ChildFileMetadata.ToArray(), path);
      CollectionAssert.AreEqual(expected.SymLinkFileIndices.ToArray(), actual.SymLinkFileIndices.ToArray(), path);
      CollectionAssert.AreEqual(expected.ChildDirectories.Select(x => x.DirectoryName.Name).ToList(),
        actual.ChildDirectories.Select(x => x.DirectoryName.Name).ToList(), path);
      for (var i = 0; i < expected.ChildDirectories.Count; i++) {
        AssertSameTree(expected.ChildDirectories[i], actual.ChildDirectories[i]);
      }
    }

    private sealed class FailingReadJournal(IChangeJournal journal) : IChangeJournal {
      public ChangeJournalQueryResult Query(FullPath directory) => journal.Query(directory);

      public void ReadRecords(FullPath directory, ChangeJournalCheckpoint start, long endUsn, ChangeJournalRecordHandler handler,
                              CancellationToken cancellationToken) {
        throw new ChangeJournalException("read error");
      }
    }

    private sealed class InactiveJournal : IChangeJournal {
      public ChangeJournalQueryResult Query(FullPath directory) => ChangeJournalQueryResult.Failure("the change journal is not active");

      public void ReadRecords(FullPath directory, ChangeJournalCheckpoint start, long endUsn, ChangeJournalRecordHandler handler,
                              CancellationToken cancellationToken) {
        throw new InvalidOperationException();
      }
    }

    private class NullProgressTrackerFactory : IProgressTrackerFactory {
      public IProgressTracker CreateTracker(int totalStepCount) => new NullProgressTracker();
      public IProgressTracker CreateIndeterminateTracker() => new NullProgressTracker();
    }

    private class NullProgressTracker : IProgressTracker {
      public bool Step() => false;
      public void DisplayProgress(DisplayProgressCallback displayProgressCallback) { }
      public void Dispose() { }
    }
  }
}
