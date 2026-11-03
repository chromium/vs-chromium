// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Features.SourceExplorerHierarchy;
using VsChromium.Protocol.Generated;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestFileSystemTree : TestServerBase {
    [TestMethod]
    public void FileSystemTreeCreationWorks() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerClient>().Value) {
          var testFile = Utils.GetChromiumTestEnlistmentFile();
          var tree = GetFileSystemFromServer(server, testFile);
          var projectPath = tree.Projects[0].RootPath;

          // Entries under the project root are file system entries.
          var entries = SendRequest(server, client => client.TreeEntriesAsync(new TreeEntriesParams {
            ProjectPath = projectPath,
            RelativePath = ""
          }));
          Assert.IsTrue(entries.Exists);
          Assert.AreEqual("", entries.RelativePath);
          var childEntries = entries.GetChildEntries();
          Assert.IsTrue(childEntries.Count > 0);
          Assert.IsTrue(childEntries.Count(x => !x.IsDirectory && x.Name == testFile.Name) > 0);
          Assert.IsTrue(childEntries.Count(x => x.IsDirectory) > 0);

          // Sub directories first, then files
          Assert.IsTrue(childEntries.TakeWhile(x => x.IsDirectory).Count() == entries.DirectoryNames.Count);

          // Multiple directories, including an unknown one
          var multiple = SendRequest(server, client => client.TreeEntriesMultipleAsync(new TreeEntriesMultipleParams {
            ProjectPath = projectPath,
            RelativePaths = { "", entries.DirectoryNames[0], "unknown-directory" }
          }));
          Assert.AreEqual(3, multiple.Directories.Count);
          Assert.IsTrue(multiple.Directories[0].Exists);
          Assert.IsTrue(multiple.Directories[1].Exists);
          Assert.IsFalse(multiple.Directories[2].Exists);
        }
      }
    }
  }
}
