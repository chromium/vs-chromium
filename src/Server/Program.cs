// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Text;
using VsChromium.Core.Logging;
using VsChromium.Core.Ipc.Json;

namespace VsChromium.Server {
  static class Program {
    /// <summary>
    /// Command line argument to speak the JSON-RPC 2.0 protocol over stdin/stdout, with the framing
    /// of the Language Server Protocol ("Content-Length" headers). See protocol/README.md.
    /// </summary>
    private const string JsonRpcArgument = "--jsonrpc";

    /// <summary>
    /// Command line argument to exchange the JSON messages of the typed messages protocol (one per
    /// line) over stdin/stdout (see <see cref="IpcStreamOverJsonLines"/>). Used by the clients until
    /// they move to the JSON-RPC protocol, then removed.
    /// </summary>
    private const string StdioArgument = "--stdio";

    private static void Main(string[] args) {
      Logger.Id = "Server";
      Logger.LogInfo("Server process started");
      try {
        if (args.Length > 0 && args[0] == JsonRpcArgument) {
          Logger.LogInfo("Server starting in JSON-RPC mode.");
          RunJsonRpc();
        } else if (args.Length > 0 && args[0] == StdioArgument) {
          Logger.LogInfo("Server starting in stdio mode (JSON lines).");
          RunOverStdio();
        } else {
          Console.Error.WriteLine("Usage: VsChromium.Server.exe {0} | {1}", JsonRpcArgument, StdioArgument);
          Console.Error.WriteLine("  {0}: JSON-RPC 2.0 over the standard input/output (\"Content-Length\" headers, as LSP).", JsonRpcArgument);
          Console.Error.WriteLine("  {0}: typed messages protocol, as JSON lines over the standard input/output (legacy).", StdioArgument);
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

      new ServerStub().RunJsonRpc(input, output);
    }

    private static void RunOverStdio() {
      var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
      var input = new StreamReader(Console.OpenStandardInput(), encoding);
      // Note: Responses are written as UTF-8 bytes directly to the output stream.
      var output = Console.OpenStandardOutput();

      // stdout is reserved for IPC messages: redirect any other console output to stderr.
      Console.SetOut(Console.Error);

      new ServerStub().Run(new IpcStreamOverJsonLines(input, output,
        (e, message) => Logger.LogError(e, "{0}", message), message => Logger.LogInfo("{0}", message)));
    }
  }
}
