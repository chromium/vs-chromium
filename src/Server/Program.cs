// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Server {
  class Program {
    private static void Main(string[] args) {
      Logger.Id = "Server";
      Logger.LogInfo("Server process started");
      try {
        var port = GetTcpPort(args);
        Logger.LogInfo("Server starting with host on port {0}.", port);
        new ServerStub().Run(port);
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in server process.");
        throw;
      }
    }

    private static int GetTcpPort(string[] args) {
      return Int32.Parse(args[0]);
    }
  }
}
