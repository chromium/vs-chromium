// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// Relative paths of directories are computed from the names of their parents (not stored):
  /// all the ways to get them must return the same paths.
  /// </summary>
  [TestClass]
  public class TestFileSystemNames {
    private static readonly FullPath Root = new FullPath(Path.Combine(Path.GetTempPath(), "root"));

    [TestMethod]
    public void RelativePathsAreComputedFromTheNames() {
      var factory = new FileSystemNameFactory();
      var root = factory.CreateAbsoluteDirectoryName(Root);
      var a = factory.CreateDirectoryName(root, "a");
      var b = factory.CreateDirectoryName(a, "bb");
      var c = factory.CreateDirectoryName(b, "ccc");
      var rootFile = factory.CreateFileName(root, "x.txt");
      var file = factory.CreateFileName(c, "foo.cc");

      Assert.AreEqual(0, root.RelativePathLength);
      Assert.IsTrue(root.RelativePath.IsEmpty);
      Assert.AreEqual("a", a.RelativePath.Value);
      Assert.AreEqual(Path.Combine("a", "bb", "ccc"), c.RelativePath.Value);
      Assert.AreEqual(c.RelativePath.Value.Length, c.RelativePathLength);
      Assert.AreEqual(Root.Combine(new RelativePath(Path.Combine("a", "bb", "ccc"))), c.FullPath);

      Assert.AreEqual("x.txt", rootFile.RelativePath.Value);
      Assert.AreEqual(Path.Combine("a", "bb", "ccc", "foo.cc"), file.RelativePath.Value);
      Assert.AreEqual(Root.Combine(new RelativePath(Path.Combine("a", "bb", "ccc", "foo.cc"))), file.FullPath);

      Assert.IsTrue(c.TestRelativePath(static (path, expected) => path.SequenceEqual(expected), Path.Combine("a", "bb", "ccc")));
      Assert.IsTrue(root.TestRelativePath(static (path, _) => path.IsEmpty, 0));
      Assert.IsTrue(file.TestRelativePath(static (path, expected) => path.SequenceEqual(expected), file.RelativePath.Value));
      Assert.IsTrue(rootFile.TestRelativePath(static (path, expected) => path.SequenceEqual(expected), "x.txt"));
      Assert.IsTrue(file.TestFullPath(static (path, expected) => path.SequenceEqual(expected), file.FullPath.Value));
      Assert.IsTrue(rootFile.TestFullPath(static (path, expected) => path.SequenceEqual(expected), rootFile.FullPath.Value));
    }

    [TestMethod]
    public void LongRelativePathsUseRentedBuffers() {
      var factory = new FileSystemNameFactory();
      DirectoryName directory = factory.CreateAbsoluteDirectoryName(Root);
      for (var i = 0; i < 60; i++) {
        directory = factory.CreateDirectoryName(directory, "directory" + i);
      }
      var file = factory.CreateFileName(directory, "file.txt");
      var expected = string.Join(Path.DirectorySeparatorChar.ToString(), Enumerable.Range(0, 60).Select(i => "directory" + i));

      Assert.IsTrue(expected.Length > DirectoryName.MaxStackPathLength);
      Assert.AreEqual(expected, directory.RelativePath.Value);
      Assert.IsTrue(directory.TestRelativePath(static (path, e) => path.SequenceEqual(e), expected));
      Assert.IsTrue(file.TestRelativePath(static (path, e) => path.SequenceEqual(e), Path.Combine(expected, "file.txt")));
      Assert.IsTrue(file.TestFullPath(static (path, e) => path.SequenceEqual(e), Root.Combine(new RelativePath(Path.Combine(expected, "file.txt"))).Value));
    }

    [TestMethod]
    public void SubtreesWithCreatedOrDeletedEntriesAreAffected() {
      var changes = new ProjectPathChanges(Root, new[] {
        new PathChangeEntry(Root, new RelativePath(Path.Combine("a", "b", "new.cc")), PathChangeKind.Created, PathKind.File),
        new PathChangeEntry(Root, new RelativePath(Path.Combine("c", "deleted")), PathChangeKind.Deleted, PathKind.Directory),
        new PathChangeEntry(Root, new RelativePath(Path.Combine("d", "changed.cc")), PathChangeKind.Changed, PathKind.File),
      });

      Assert.IsTrue(changes.IsSubtreeAffected(RelativePath.Empty));
      Assert.IsTrue(changes.IsSubtreeAffected(new RelativePath("a")));
      Assert.IsTrue(changes.IsSubtreeAffected(new RelativePath(Path.Combine("a", "b"))));
      Assert.IsTrue(changes.IsSubtreeAffected(new RelativePath("c")));
      // Deleted directory: its own subtree is gone, its parent is affected.
      Assert.IsFalse(changes.IsSubtreeAffected(new RelativePath(Path.Combine("c", "deleted"))));
      // Content changes don't change the tree.
      Assert.IsFalse(changes.IsSubtreeAffected(new RelativePath("d")));
      Assert.IsFalse(changes.IsSubtreeAffected(new RelativePath("e")));
    }
  }
}
