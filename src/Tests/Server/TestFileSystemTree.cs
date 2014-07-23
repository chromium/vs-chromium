// Copyright 2014 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileSystemTree : TestServerBase {
    [TestMethod]
    public void FileSystemTreeCreationWorks() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          var tree = GetFileSystemFromServer(server, testFile);
          var chromiumEntry = tree.Root.Entries[0] as DirectoryEntry;
          Assert.IsNotNull(chromiumEntry);

          // Entries under Chromium entry are file system entries.
          Assert.IsTrue(chromiumEntry.Entries.Count > 0);
          Assert.IsTrue(chromiumEntry.Entries.Count(x => x.Name == testFile.Name) > 0);
        }
      }
    }
  }
}
