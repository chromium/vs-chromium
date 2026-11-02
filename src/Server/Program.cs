// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Text;
using VsChromium.Core.Logging;
using VsChromium.Server.Ipc.Json;

namespace VsChromium.Server {
  class Program {
    /// <summary>
    /// Command line argument to exchange JSON messages (one per line) over stdin/stdout instead of
    /// connecting to a TCP port (see <see cref="IpcStreamOverJsonLines"/>).
    /// </summary>
    private const string StdioArgument = "--stdio";

    private static void Main(string[] args) {
      Logger.Id = "Server";
      Logger.LogInfo("Server process started");
      try {
        if (args.Length > 0 && args[0] == StdioArgument) {
          Logger.LogInfo("Server starting in stdio mode (JSON lines).");
          RunOverStdio();
        } else {
          var port = GetTcpPort(args);
          Logger.LogInfo("Server starting with host on port {0}.", port);
          new ServerStub().Run(port);
        }
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in server process.");
        throw;
      }
    }

    private static void RunOverStdio() {
      var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
      var input = new StreamReader(Console.OpenStandardInput(), encoding);
      var output = new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = false };

      // stdout is reserved for IPC messages: redirect any other console output to stderr.
      Console.SetOut(Console.Error);

      new ServerStub().Run(new IpcStreamOverJsonLines(input, output));
    }

    private static int GetTcpPort(string[] args) {
      return Int32.Parse(args[0]);
    }
  }
}
