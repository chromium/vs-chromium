// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestUnregisterFile : TestServerBase {
    [TestMethod]
    public void UnregisterFileRequestWorks() {
      var testFile = Utils.GetChromiumTestEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerClient>().Value) {
          GetFileSystemFromServer(server, testFile);
          SendFileSystemRequest(server, testFile, SendUnregisterFileRequest);
          var tree = SendGetFileSystemTreeRequest(server);
          Assert.AreEqual(0, tree.Projects.Count);
        }
      }
    }
  }
}
