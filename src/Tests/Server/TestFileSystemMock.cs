// Copyright 2015 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Tests.Mocks;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileSystemMock {

    [TestMethod]
    public void DiscoverObsoleteProjectFileWorks() {
      var fileSystem = new FileSystemMock();
      fileSystem
        .AddDirectory(@"d:\foo")
        .AddFile(@"bar.txt", "some text content")
        .Parent.AddDirectory(@"bar");

      Assert.IsFalse(fileSystem.GetFileInfoSnapshot(new FullPath(@"d:\foo\bar2")).Exists);
      Assert.IsTrue(fileSystem.GetFileInfoSnapshot(new FullPath(@"d:\foo\bar")).IsDirectory);
      Assert.IsTrue(fileSystem.GetFileInfoSnapshot(new FullPath(@"d:\foo\bar.txt")).IsFile);
    }
  }
}
