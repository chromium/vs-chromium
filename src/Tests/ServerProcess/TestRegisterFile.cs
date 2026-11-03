// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.ServerProxy;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestRegisterFile : TestServerBase {
    [TestMethod]
    public void RegisterFileRequestWorks() {
      var testFile = Utils.GetChromiumTestEnlistmentFile();

      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerClient>().Value) {
          // Send "files/register" request, and wait for response.
          // We don't care if the request is processed properly.
          SendRegisterFileRequest(server, testFile);
        }
      }
    }

    [TestMethod]
    public void InitializeWorks() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerClient>().Value) {
          Assert.IsFalse(server.IsServerRunning);
          var started = false;
          server.ProcessStarted += (sender, args) => started = true;

          // The first request starts the server and sends "server/initialize".
          var statistics = SendRequest(server, client => client.ServerStatisticsAsync(new Protocol.Generated.ServerStatisticsParams()));
          Assert.IsNotNull(statistics);
          Assert.IsTrue(statistics.ProcessId > 0);
          Assert.IsTrue(started);
          Assert.IsTrue(server.IsServerRunning);
        }
      }
    }
  }
}
