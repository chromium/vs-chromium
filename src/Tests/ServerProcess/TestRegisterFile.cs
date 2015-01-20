// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestRegisterFile : TestServerBase {
    [TestMethod]
    public void RegisterFileRequestWorks() {
      var testFile = GetChromiumEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<ITypedRequestProcessProxy>().Value) {
          // Send "AddFile" request, and wait for response.
          // We don't care if the request is processed properly.
          var response = SendRegisterFileRequest(server, testFile, ServerResponseTimeout)();
          Assert.IsNotNull(response, "Server did not respond within timeout.");
        }
      }
    }
  }
}
