// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Linq;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystem.Builder;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestSnapshotDiff {
    private static readonly DateTime Time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly FullPath Root = new FullPath(Path.Combine(Path.GetTempPath(), "VsChromiumSnapshotDiff"));
    private readonly FileSystemNameFactory _factory = new FileSystemNameFactory();

    /// <summary>
    /// A directory of a test tree: files (name => size, time offset in seconds) and children.
    /// </summary>
    private sealed class Dir {
      public string Name;
      public bool IsSymLink;
      public List<(string Name, long Size, int Seconds)> Files = new List<(string, long, int)>();
      public List<Dir> Children = new List<Dir>();

      public Dir(string name, bool isSymLink = false) {
        Name = name;
        IsSymLink = isSymLink;
      }

      public Dir File(string name, long size = 10, int seconds = 0) {
        Files.Add((name, size, seconds));
        return this;
      }

      public Dir Child(Dir child) {
        Children.Add(child);
        return this;
      }
    }

    private DirectorySnapshot Build(Dir root) {
      return Build(root, _factory.CreateAbsoluteDirectoryName(Root));
    }

    private DirectorySnapshot Build(Dir dir, DirectoryName name) {
      var files = dir.Files.OrderBy(x => x.Name, SystemPathComparer.NameComparer).ToList();
      var children = dir.Children
        .OrderBy(x => x.Name, SystemPathComparer.NameComparer)
        .Select(x => Build(x, _factory.CreateDirectoryName(name, x.Name, x.IsSymLink)))
        .ToReadOnlyList();
      return new DirectorySnapshot(new DirectoryData(name, dir.IsSymLink), children,
        files.Select(x => _factory.CreateFileName(name, x.Name)).ToReadOnlyList(),
        files.Select(x => new FileMetadata(Time.AddSeconds(x.Seconds), x.Size)).ToArray());
    }

    private static Dir Tree() {
      return new Dir("")
        .File("a.cc").File("b.cc")
        .Child(new Dir("src").File("c.cc").Child(new Dir("deep").File("d.cc")))
        .Child(new Dir("link", isSymLink: true).File("e.cc"));
    }

    private static string[] Describe(List<PathChangeEntry> changes) {
      return changes.Select(x => $"{x.ChangeKind} {x.PathKind} {x.RelativePath.Value}").OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string P(params string[] names) => Path.Combine(names);

    [TestMethod]
    public void SameTreesHaveNoChanges() {
      var changes = SnapshotDiff.Compute(Root, Build(Tree()), Build(Tree()), null, CancellationToken.None);
      Assert.AreEqual(0, changes.Count);
    }

    [TestMethod]
    public void FilesWithAnotherTimeOrSizeAreChanged() {
      var newTree = Tree();
      newTree.Files[0] = ("a.cc", 10, 1); // same size, other time
      newTree.Children[0].Children[0].Files[0] = ("d.cc", 11, 0); // other size, same time
      CollectionAssert.AreEqual(new[] { "Changed File a.cc", $"Changed File {P("src", "deep", "d.cc")}" },
        Describe(SnapshotDiff.Compute(Root, Build(Tree()), Build(newTree), null, CancellationToken.None)));
    }

    [TestMethod]
    public void CreatedAndDeletedFiles() {
      var newTree = Tree();
      newTree.Files.RemoveAt(1); // b.cc deleted
      newTree.Children[0].File("new.cc");
      CollectionAssert.AreEqual(new[] { $"Created File {P("src", "new.cc")}", "Deleted File b.cc" },
        Describe(SnapshotDiff.Compute(Root, Build(Tree()), Build(newTree), null, CancellationToken.None)));
    }

    [TestMethod]
    public void CreatedDeletedAndRenamedDirectories() {
      var newTree = Tree();
      newTree.Children[0].Children.Clear(); // src/deep deleted
      newTree.Child(new Dir("new").File("f.cc"));
      newTree.Children[0].Name = "renamed"; // src renamed
      CollectionAssert.AreEqual(new[] { "Created Directory new", "Created Directory renamed", "Deleted Directory src" },
        Describe(SnapshotDiff.Compute(Root, Build(Tree()), Build(newTree), null, CancellationToken.None)));
    }

    [TestMethod]
    public void DirectoryReplacedWithASymbolicLinkIsRecreated() {
      var newTree = Tree();
      newTree.Children[1].IsSymLink = false;
      CollectionAssert.AreEqual(new[] { "Created Directory link", "Deleted Directory link" },
        Describe(SnapshotDiff.Compute(Root, Build(Tree()), Build(newTree), null, CancellationToken.None)));
    }

    [TestMethod]
    public void FilesWithoutContentsAreChanged() {
      var oldRoot = Build(Tree());
      var large = oldRoot.ChildDirectories.Single(x => x.DirectoryName.Name == "src").ChildFiles[0]; // src/c.cc
      var changes = SnapshotDiff.Compute(Root, oldRoot, Build(Tree()), new HashSet<FileName> { large }, CancellationToken.None);
      CollectionAssert.AreEqual(new[] { $"Changed File {P("src", "c.cc")}" }, Describe(changes));
    }

    [TestMethod]
    public void ChangesSelectTheFilesToLoad() {
      var newTree = Tree();
      newTree.Files[0] = ("a.cc", 10, 1);
      newTree.Child(new Dir("new").Child(new Dir("sub").File("g.cc")));
      var newRoot = Build(newTree);
      var changes = new FullPathChanges(SnapshotDiff.Compute(Root, Build(Tree()), newRoot, null, CancellationToken.None));
      Assert.IsFalse(changes.ShouldSkipLoadFileContents(newRoot.ChildFiles[0]), "a.cc changed");
      Assert.IsTrue(changes.ShouldSkipLoadFileContents(newRoot.ChildFiles[1]), "b.cc unchanged");
      var g = newRoot.ChildDirectories.Single(x => x.DirectoryName.Name == "new").ChildDirectories[0].ChildFiles[0];
      Assert.IsFalse(changes.ShouldSkipLoadFileContents(g), "file of a created directory");
    }
  }
}
