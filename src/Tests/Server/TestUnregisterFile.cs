// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestUnregisterFile : TestServerBase {
    [TestMethod]
    public void UnregisterFileRequestWorks() {
      var testFile = GetChromiumEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          GetFileSystemFromServer(server, testFile);
          SendFileSystemRequest(server, testFile, SendUnregisterFileRequest);
          var tree = SendGetFileSystemRequest(server);
          AssertTreeIsEmpty(tree);
        }
      }
    }

    private void AssertTreeIsEmpty(FileSystemTree tree) {
      Assert.IsNotNull(tree.Root);
      Assert.IsNotNull(tree.Root.Name);
      Assert.AreEqual("", tree.Root.Name);
      Assert.IsNotNull(tree.Root.Entries);
      Assert.AreEqual(0, tree.Root.Entries.Count);
    }
  }
}
