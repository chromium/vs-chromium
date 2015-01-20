// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.ServerProxy;

namespace VsChromium.Tests.ServerProcess {
  [TestClass]
  public class TestEchoProtocol : MefBaseTest {
    [TestMethod]
    public void ServerResponds() {
      using (var container = SetupMefContainer()) {
        using (var server = container.GetExport<IServerProcessProxy>().Value) {
          var waitHandle = new EventWaitHandle(false, EventResetMode.ManualReset);
          var request = new IpcRequest {
            RequestId = 5,
            Protocol = IpcProtocols.Echo,
            Data = new IpcStringData {
              Text = "sdfsdsd"
            }
          };
          IpcResponse response = null;
          server.RunAsync(request, x => {
            response = x;
            waitHandle.Set();
          });
          Assert.IsTrue(waitHandle.WaitOne(TimeSpan.FromSeconds(5.0)), "Server did not respond within 5 seconds.");
          Assert.AreEqual(5, response.RequestId);
          Assert.AreEqual(IpcProtocols.Echo, response.Protocol);
          Assert.IsNotNull(request.Data);
          Assert.IsNotNull(response.Data);
          Assert.AreEqual(request.Data.GetType(), typeof(IpcStringData));
          Assert.AreEqual(response.Data.GetType(), typeof(IpcStringData));
          Assert.AreEqual((request.Data as IpcStringData).Text, (response.Data as IpcStringData).Text);
        }
      }
    }
  }
}
