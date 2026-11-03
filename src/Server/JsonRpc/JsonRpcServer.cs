// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.IO;
using VsChromium.Core.Logging;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// The server in JSON-RPC mode ("--jsonrpc"): processes the requests of the client until it
  /// closes the input stream, and forwards the events of the server as notifications.
  /// </summary>
  public sealed class JsonRpcServer {
    private readonly JsonRpcOutput _output;
    private readonly JsonRpcConnection _connection;
    private readonly JsonRpcEventForwarder _eventForwarder;

    public JsonRpcServer(JsonRpcOutput output, JsonRpcConnection connection, JsonRpcEventForwarder eventForwarder) {
      _output = output;
      _connection = connection;
      _eventForwarder = eventForwarder;
    }

    /// <summary>
    /// Processes the messages of <paramref name="input"/> until the end of the stream, writing the
    /// responses and notifications to <paramref name="output"/>.
    /// </summary>
    public void Run(Stream input, Stream output) {
      _output.Connect(output);
      _eventForwarder.RegisterEventHandlers();
      _connection.Run(input);
      // The client closed the input stream: there is nothing left for the server to do.
      Logger.LogInfo("Server terminating properly.");
    }
  }
}
