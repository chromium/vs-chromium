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
    private readonly PortableFileSystem _fileSystem = new PortableFileSystem();

    [TestInitialize]
    public void Initialize() {
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

    private FullPath RootPath => new FullPath(_root);

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
  }
}
