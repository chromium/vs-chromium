// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using VsChromium.Core.Logging;

namespace VsChromium.Server {
  static class Program {
    /// <summary>
    /// Command line argument to speak the JSON-RPC 2.0 protocol over stdin/stdout, with the framing
    /// of the Language Server Protocol ("Content-Length" headers). See protocol/README.md.
    /// </summary>
    private const string JsonRpcArgument = "--jsonrpc";

    /// <summary>
    /// Alias of <see cref="JsonRpcArgument"/> (the usual convention of the Language Server Protocol).
    /// </summary>
    private const string StdioArgument = "--stdio";

    private static void Main(string[] args) {
      Logger.Id = "Server";
      Logger.LogInfo("Server process started");
      try {
        if (args.Length > 0 && (args[0] == JsonRpcArgument || args[0] == StdioArgument)) {
          Logger.LogInfo("Server starting (JSON-RPC over stdin/stdout).");
          RunJsonRpc();
        } else {
          Console.Error.WriteLine("Usage: VsChromium.Server.exe {0} | {1}", JsonRpcArgument, StdioArgument);
          Console.Error.WriteLine("  JSON-RPC 2.0 over the standard input/output (\"Content-Length\" headers, as LSP).");
          Console.Error.WriteLine("  {0} is an alias of {1}.", StdioArgument, JsonRpcArgument);
          Console.Error.WriteLine("  The server exits when its standard input is closed.");
          Environment.ExitCode = 1;
        }
      }
      catch (Exception e) {
        Logger.LogError(e, "Error in server process.");
        throw;
      }
    }

    private static void RunJsonRpc() {
      var input = Console.OpenStandardInput();
      var output = Console.OpenStandardOutput();

      // stdout is reserved for protocol messages: redirect any other console output to stderr.
      Console.SetOut(Console.Error);

      new ServerStub().Run(input, output);
    }
  }
}
