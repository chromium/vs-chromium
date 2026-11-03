// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Text.Json;
using VsChromium.Core.Logging;

namespace VsChromium.Server.JsonRpc {
  /// <summary>
  /// The output side of the JSON-RPC connection with the client: writes the responses (see <see
  /// cref="JsonRpcConnection"/>) and the notifications of the server (<see cref="SendNotification"/>),
  /// from any thread. Separate from the connection so that the services (e.g. progress
  /// notifications) don't depend on the request handler.
  /// </summary>
  public sealed class JsonRpcOutput {
    public static readonly JsonWriterOptions WriterOptions = new JsonWriterOptions { SkipValidation = true };

    private volatile JsonRpcMessageWriter _writer;

    public bool IsConnected => _writer != null;

    /// <summary>
    /// Sets the stream messages are written to. Notifications sent before are dropped.
    /// </summary>
    public void Connect(Stream output) {
      _writer = new JsonRpcMessageWriter(output);
    }

    /// <summary>Sends a notification to the client.</summary>
    public void SendNotification(string method, JsonElement? parameters) {
      if (_writer == null) {
        return;
      }
      using (var message = new JsonRpcMessageBuffer()) {
        using (var json = new Utf8JsonWriter(message, WriterOptions)) {
          json.WriteStartObject();
          json.WriteString("jsonrpc", "2.0");
          json.WriteString("method", method);
          if (parameters is { } value) {
            json.WritePropertyName("params");
            value.WriteTo(json);
          }
          json.WriteEndObject();
        }
        Write(message);
      }
    }

    /// <summary>
    /// Writes a message. Errors (e.g. the client is gone) are logged: the server exits when its
    /// input is closed.
    /// </summary>
    public void Write(JsonRpcMessageBuffer message) {
      var writer = _writer;
      if (writer == null) {
        throw new InvalidOperationException("The JSON-RPC output is not connected");
      }
      try {
        writer.WriteMessage(message);
      }
      catch (Exception e) when (e is IOException || e is ObjectDisposedException) {
        Logger.LogError(e, "Error writing a JSON-RPC message to the client.");
      }
    }
  }
}
