// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromiumCore.Ipc.TypedMessages;
using VsChromiumPackage.Server;

namespace VsChromiumTests.Server {
  [TestClass]
  public class TestAddFile : TestServerBase {
    [TestMethod]
    public void AddFileRequestWorks() {
      var testFile = GetChromiumEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          // Send "AddFile" request, and wait for response.
          // We don't care if the request is processed properly.
          var response = SendAddFileRequest(server, testFile, ServerResponseTimeout)();
          Assert.IsNotNull(response, "Server did not respond within timeout.");
        }
      }
    }

    [TestMethod]
    public void FileSystemTreeCreationWorks() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          var testFile = GetChromiumEnlistmentFile();
          var tree = GetFileSystemTreeFromServer(server, testFile);
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
