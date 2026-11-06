// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Files.Windows;

namespace VsChromium.Tests.Core {
  /// <summary>
  /// The tests of <see cref="TestPortableFileSystem"/> on <see cref="WindowsFileSystem"/>, and the
  /// file ids of the entries.
  /// </summary>
  [TestClass]
  [SupportedOSPlatform("windows")]
  public class TestWindowsFileSystem : TestPortableFileSystem {
    protected override PortableFileSystem CreateFileSystem() {
      if (!OperatingSystem.IsWindows())
        Assert.Inconclusive("Windows only");
      return new WindowsFileSystem();
    }

    [TestMethod]
    public void GetDirectoryEntriesReturnsTheSameEntriesAsPortableFileSystem() {
      // More than one buffer of entries (64 KB).
      for (var i = 0; i < 1500; i++) {
        File.WriteAllText(Path.Combine(Root, "file-with-a-long-name-to-fill-the-buffer-" + i + ".txt"), new string('x', i));
      }
      for (var i = 0; i < 20; i++) {
        Directory.CreateDirectory(Path.Combine(Root, "dir" + i));
      }
      File.SetAttributes(Path.Combine(Root, "file-with-a-long-name-to-fill-the-buffer-7.txt"), FileAttributes.Hidden | FileAttributes.ReadOnly);

      var expected = new PortableFileSystem().GetDirectoryEntries(RootPath).OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
      var actual = FileSystem.GetDirectoryEntries(RootPath).OrderBy(x => x.Name, StringComparer.Ordinal).ToList();
      Assert.AreEqual(1520, actual.Count);
      CollectionAssert.AreEqual(expected.Select(x => x.Name).ToList(), actual.Select(x => x.Name).ToList());
      CollectionAssert.AreEqual(expected.Select(x => x.Metadata).ToList(), actual.Select(x => x.Metadata).ToList());
      CollectionAssert.AreEqual(expected.Select(x => (x.IsFile, x.IsDirectory, x.IsSymLink)).ToList(),
        actual.Select(x => (x.IsFile, x.IsDirectory, x.IsSymLink)).ToList());
    }

    [TestMethod]
    public void GetDirectoryEntriesReturnsTheFileIds() {
      Directory.CreateDirectory(Path.Combine(Root, "a"));
      Directory.CreateDirectory(Path.Combine(Root, "b"));
      File.WriteAllText(Path.Combine(Root, "c.txt"), "c");
      var entries = FileSystem.GetDirectoryEntries(RootPath);
      Assert.AreEqual(3, entries.Count);
      Assert.AreEqual(3, entries.Select(x => x.FileId).Distinct().Count());
      foreach (var entry in entries) {
        Assert.AreNotEqual(UInt128.Zero, entry.FileId, entry.Name);
        Assert.AreEqual(WindowsFileSystem.GetFileId(Path.Combine(Root, entry.Name)), entry.FileId, entry.Name);
        Assert.AreEqual(entry.FileId, FileSystem.GetFileInfoSnapshot(RootPath.Combine(new RelativePath(entry.Name))).FileId);
      }

      // Same id after a rename, another id for a new directory with the same name.
      var id = entries.Single(x => x.Name == "a").FileId;
      Directory.Move(Path.Combine(Root, "a"), Path.Combine(Root, "renamed"));
      Directory.CreateDirectory(Path.Combine(Root, "a"));
      entries = FileSystem.GetDirectoryEntries(RootPath);
      Assert.AreEqual(id, entries.Single(x => x.Name == "renamed").FileId);
      Assert.AreNotEqual(id, entries.Single(x => x.Name == "a").FileId);
    }

    [TestMethod]
    public void GetDirectoryEntriesWorksWithLongPaths() {
      var path = Root;
      while (path.Length < 300) {
        path = Path.Combine(path, "a-directory-with-a-long-name");
      }
      Directory.CreateDirectory(path);
      File.WriteAllText(Path.Combine(path, "file.txt"), "contents");
      var entry = FileSystem.GetDirectoryEntries(new FullPath(path)).Single();
      Assert.AreEqual("file.txt", entry.Name);
      Assert.AreEqual(8L, entry.Metadata.Length);
      Assert.AreNotEqual(UInt128.Zero, entry.FileId);
    }

    [TestMethod]
    public void GetDirectoryEntriesOfFileThrows() {
      var path = Path.Combine(Root, "file.txt");
      File.WriteAllText(path, "contents");
      Assert.Throws<IOException>(() => FileSystem.GetDirectoryEntries(new FullPath(path)));
    }

    [TestMethod]
    public void SymLinkFileMetadataIsTheMetadataOfTheEnumeration() {
      System.IO.Directory.CreateDirectory(Path.Combine(Root, "tools"));
      var target = Path.Combine(Root, "tools", "target.txt");
      File.WriteAllText(target, "contents of the target");
      File.SetLastWriteTimeUtc(target, new DateTime(2026, 8, 19, 14, 39, 41, DateTimeKind.Utc));
      CreateFileSymbolicLink(Path.Combine(Root, "link.txt"), Path.Combine("tools", "target.txt"));
      CreateFileSymbolicLink(Path.Combine(Root, "dangling.txt"), "missing.txt");
      var entries = FileSystem.GetDirectoryEntries(RootPath);
      var portable = new PortableFileSystem();
      foreach (var name in new[] { "link.txt", "dangling.txt" }) {
        var path = RootPath.Combine(new RelativePath(name));
        var expected = portable.GetDirectoryEntries(RootPath).Single(x => x.Name == name).Metadata;
        Assert.AreEqual(expected, entries.Single(x => x.Name == name).Metadata, name);
        Assert.AreEqual(expected, ((IFileSystem)FileSystem).GetSymLinkFileMetadata(path), name);
        Assert.AreEqual(expected, ((IFileSystem)portable).GetSymLinkFileMetadata(path), name);
      }
      Assert.AreEqual(new FileMetadata(File.GetLastWriteTimeUtc(target), new FileInfo(target).Length),
        ((IFileSystem)FileSystem).GetSymLinkFileMetadata(RootPath.Combine(new RelativePath("link.txt"))));
      Assert.IsNull(((IFileSystem)FileSystem).GetSymLinkFileMetadata(RootPath.Combine(new RelativePath("missing"))));
    }

    [TestMethod]
    public void FileIdOfMissingFileIsZero() {
      Assert.AreEqual(UInt128.Zero, FileSystem.GetFileInfoSnapshot(RootPath.Combine(new RelativePath("missing"))).FileId);
    }
  }
}
