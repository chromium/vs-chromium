// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFullPathChanges {
    [TestMethod]
    public void ShouldSkipLoadFileContentsOfFileNameIsSameAsOfFullPath() {
      var rootPath = new FullPath(Path.Combine(Path.GetTempPath(), "VsChromiumRoot"));
      var changes = new FullPathChanges(new List<PathChangeEntry> {
        new PathChangeEntry(rootPath, new RelativePath(Path.Combine("src", "changed.cc")), PathChangeKind.Changed, PathKind.File),
        new PathChangeEntry(rootPath, new RelativePath(Path.Combine("src", "NewDir")), PathChangeKind.Created, PathKind.Directory),
      });

      var factory = new FileSystemNameFactory();
      var root = factory.CreateAbsoluteDirectoryName(rootPath);
      var src = factory.CreateDirectoryName(root, "src");
      var newDir = factory.CreateDirectoryName(src, "newdir"); // Different case
      var newDirChild = factory.CreateDirectoryName(newDir, "child");
      var other = factory.CreateDirectoryName(root, "other");
      var files = new[] {
        factory.CreateFileName(src, "changed.cc"),
        factory.CreateFileName(src, "CHANGED.cc"),
        factory.CreateFileName(src, "unchanged.cc"),
        factory.CreateFileName(newDir, "a.cc"),
        factory.CreateFileName(newDirChild, "b.cc"),
        factory.CreateFileName(other, "c.cc"),
        factory.CreateFileName(root, "top.cc"),
      };
      foreach (var file in files) {
        Assert.AreEqual(changes.ShouldSkipLoadFileContents(file.FullPath), changes.ShouldSkipLoadFileContents(file), file.FullPath.Value);
      }
      Assert.IsFalse(changes.ShouldSkipLoadFileContents(files[0]));
      Assert.IsTrue(changes.ShouldSkipLoadFileContents(files[2]));
      Assert.IsTrue(changes.ShouldSkipLoadFileContents(files[5]));
    }

    [TestMethod]
    public void TestFullPathIsSameAsFullPath() {
      var factory = new FileSystemNameFactory();
      // Root with and without a trailing separator
      foreach (var rootPath in new[] { Path.GetPathRoot(Path.GetTempPath()), Path.Combine(Path.GetTempPath(), "VsChromiumRoot") }) {
        var root = factory.CreateAbsoluteDirectoryName(new FullPath(rootPath));
        var dir = factory.CreateDirectoryName(factory.CreateDirectoryName(root, "a"), "b");
        foreach (var file in new[] { factory.CreateFileName(root, "x.cc"), factory.CreateFileName(dir, "y.cc") }) {
          string built = null;
          file.TestFullPath((path, _) => { built = path.ToString(); return true; }, 0);
          Assert.AreEqual(file.FullPath.Value, built);
        }
      }
    }
  }
}
