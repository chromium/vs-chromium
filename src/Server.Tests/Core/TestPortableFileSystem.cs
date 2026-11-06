// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestPortableFileSystem {
    private string _root;
    private PortableFileSystem _fileSystem;

    /// <summary>The implementation tested (the tests of <see cref="TestWindowsFileSystem"/> run the same tests).</summary>
    protected virtual PortableFileSystem CreateFileSystem() {
      return new PortableFileSystem();
    }

    protected PortableFileSystem FileSystem => _fileSystem;

    protected string Root => _root;

    [TestInitialize]
    public void Initialize() {
      _fileSystem = CreateFileSystem();
      _root = Path.Combine(Path.GetTempPath(), "VsChromium.Tests", "PortableFileSystem-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() {
      try {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) {
          File.SetAttributes(file, FileAttributes.Normal);
        }
        Directory.Delete(_root, recursive: true);
      }
      catch (IOException) {
        // Best effort
      }
    }

    protected FullPath RootPath => new FullPath(_root);

    [TestMethod]
    public void FileInfoSnapshotOfFileWorks() {
      var path = Path.Combine(_root, "file.txt");
      File.WriteAllText(path, "hello");
      var info = _fileSystem.GetFileInfoSnapshot(new FullPath(path));
      Assert.IsTrue(info.Exists);
      Assert.IsTrue(info.IsFile);
      Assert.IsFalse(info.IsDirectory);
      Assert.IsFalse(info.IsSymLink);
      Assert.AreEqual(5L, info.Length);
      Assert.AreEqual(File.GetLastWriteTimeUtc(path), info.LastWriteTimeUtc);
    }

    [TestMethod]
    public void FileInfoSnapshotOfDirectoryWorks() {
      var info = _fileSystem.GetFileInfoSnapshot(RootPath);
      Assert.IsTrue(info.Exists);
      Assert.IsFalse(info.IsFile);
      Assert.IsTrue(info.IsDirectory);
      Assert.AreEqual(Directory.GetLastWriteTimeUtc(_root), info.LastWriteTimeUtc);
    }

    [TestMethod]
    public void FileInfoSnapshotOfMissingFileWorks() {
      var info = _fileSystem.GetFileInfoSnapshot(RootPath.Combine(new RelativePath("missing.txt")));
      Assert.IsFalse(info.Exists);
      Assert.IsFalse(info.IsFile);
      Assert.IsFalse(info.IsDirectory);
      Assert.ThrowsExactly<Exception>(() => { var unused = info.Length; });
      Assert.ThrowsExactly<Exception>(() => { var unused = info.LastWriteTimeUtc; });

      var info2 = _fileSystem.GetFileInfoSnapshot(RootPath.Combine(new RelativePath(@"missing-dir\missing.txt")));
      Assert.IsFalse(info2.Exists);
    }

    [TestMethod]
    public void ReadFileNulTerminatedWorks() {
      var path = Path.Combine(_root, "file.txt");
      var bytes = Encoding.UTF8.GetBytes("hello world");
      File.WriteAllBytes(path, bytes);
      using (var block = _fileSystem.ReadFileNulTerminated(new FullPath(path), bytes.Length, 2)) {
        Assert.AreEqual(bytes.Length + 2, block.ByteLength);
        var contents = new byte[block.ByteLength];
        Marshal.Copy(block.Pointer, contents, 0, contents.Length);
        CollectionAssert.AreEqual(bytes.Concat(new byte[] { 0, 0 }).ToArray(), contents);
      }
    }

    [TestMethod]
    public void ReadFileNulTerminatedWorksWhileFileIsOpenForWriting() {
      var path = Path.Combine(_root, "file.txt");
      using (var stream = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete)) {
        stream.Write(new byte[] { 1, 2, 3 });
        stream.Flush();
        using (var block = _fileSystem.ReadFileNulTerminated(new FullPath(path), 3, 2)) {
          Assert.AreEqual(5, block.ByteLength);
          Assert.AreEqual((byte)3, Marshal.ReadByte(block.Pointer, 2));
        }
      }
    }

    [TestMethod]
    public void ReadFileNulTerminatedWithAttributesWorks() {
      var path = Path.Combine(_root, "file.txt");
      var bytes = Encoding.UTF8.GetBytes("hello world");
      File.WriteAllBytes(path, bytes);
      using (var block = _fileSystem.ReadFileNulTerminated(new FullPath(path), 2, out var lastWriteTimeUtc)) {
        Assert.AreEqual(bytes.Length + 2, block.ByteLength);
        Assert.AreEqual(File.GetLastWriteTimeUtc(path), lastWriteTimeUtc);
        Assert.AreEqual((byte)'d', Marshal.ReadByte(block.Pointer, bytes.Length - 1));
        Assert.AreEqual((byte)0, Marshal.ReadByte(block.Pointer, bytes.Length));
      }
    }

    [TestMethod]
    public void ReadFileNulTerminatedOfMissingFileThrows() {
      Assert.Throws<IOException>(() =>
        _fileSystem.ReadFileNulTerminated(RootPath.Combine(new RelativePath("missing.txt")), 10, 2));
    }

    [TestMethod]
    public void GetDirectoryEntriesWorks() {
      File.WriteAllText(Path.Combine(_root, "a.txt"), "a");
      File.WriteAllText(Path.Combine(_root, ".hidden"), "h");
      File.SetAttributes(Path.Combine(_root, ".hidden"), FileAttributes.Hidden);
      Directory.CreateDirectory(Path.Combine(_root, "subdir"));
      File.WriteAllText(Path.Combine(_root, "subdir", "b.txt"), "b");

      var entries = _fileSystem.GetDirectoryEntries(RootPath).OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
      CollectionAssert.AreEqual(new[] { ".hidden", "a.txt", "subdir" }, entries.Select(x => x.Name).ToArray());
      Assert.IsTrue(entries[0].IsFile);
      Assert.IsTrue(entries[1].IsFile);
      Assert.IsTrue(entries[2].IsDirectory);
      Assert.IsFalse(entries.Any(x => x.IsSymLink));
    }

    [TestMethod]
    public void GetDirectoryEntriesOfMissingDirectoryThrows() {
      Assert.Throws<IOException>(() => _fileSystem.GetDirectoryEntries(RootPath.Combine(new RelativePath("missing"))));
    }

    /// <summary>
    /// Creates a file symbolic link (relative target), or marks the test inconclusive if symbolic
    /// links can't be created (Windows without developer mode or elevation).
    /// </summary>
    protected static void CreateFileSymbolicLink(string linkPath, string relativeTarget) {
      try {
        File.CreateSymbolicLink(linkPath, relativeTarget);
      }
      catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }
    }

    [TestMethod]
    public void GetDirectoryEntriesReturnsTheMetadataOfTheTargetOfFileSymbolicLinks() {
      Directory.CreateDirectory(Path.Combine(_root, "tools"));
      var target = Path.Combine(_root, "tools", "target.txt");
      File.WriteAllText(target, "contents of the target");
      File.SetLastWriteTimeUtc(target, new DateTime(2026, 8, 19, 14, 39, 41, DateTimeKind.Utc));
      CreateFileSymbolicLink(Path.Combine(_root, "link.txt"), Path.Combine("tools", "target.txt"));

      var link = _fileSystem.GetDirectoryEntries(RootPath).Single(x => x.Name == "link.txt");
      Assert.IsTrue(link.IsFile);
      Assert.IsTrue(link.IsSymLink);
      Assert.AreEqual(new FileMetadata(File.GetLastWriteTimeUtc(target), new FileInfo(target).Length), link.Metadata);

      // The metadata of the contents read through the link are the same (index cache match).
      using (var block = _fileSystem.ReadFileNulTerminated(RootPath.Combine(new RelativePath("link.txt")), 2, out var readTime)) {
        Assert.AreEqual(link.Metadata.LastWriteTimeUtc, readTime);
        Assert.AreEqual(link.Metadata.Length + 2, block.ByteLength);
      }

      // A change of the target is a change of the link.
      File.WriteAllText(target, "new contents of the target, longer");
      var changed = _fileSystem.GetDirectoryEntries(RootPath).Single(x => x.Name == "link.txt");
      Assert.AreEqual(new FileInfo(target).Length, changed.Metadata.Length);
      Assert.AreNotEqual(link.Metadata, changed.Metadata);

      var info = _fileSystem.GetFileInfoSnapshot(RootPath.Combine(new RelativePath("link.txt")));
      Assert.IsTrue(info.IsSymLink);
      Assert.AreEqual(changed.Metadata, new FileMetadata(info.LastWriteTimeUtc, info.Length));
    }

    [TestMethod]
    public void GetDirectoryEntriesKeepsTheMetadataOfDanglingFileSymbolicLinks() {
      CreateFileSymbolicLink(Path.Combine(_root, "dangling.txt"), "missing.txt");
      var link = _fileSystem.GetDirectoryEntries(RootPath).Single(x => x.Name == "dangling.txt");
      Assert.IsTrue(link.IsFile);
      Assert.IsTrue(link.IsSymLink);
      Assert.AreEqual(0L, link.Metadata.Length);
    }
  }
}
