// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using VsChromium.Core.Ipc;
using VsChromium.Core.Logging;

namespace VsChromium.Server.Ipc.Json {
  /// <summary>
  /// <see cref="IIpcStream"/> implementation exchanging JSON messages (see <see
  /// cref="IpcJsonSerializer"/>), one message per line ("JSON lines"). Used for the
  /// <c>--stdio</c> mode of the server, where messages are exchanged over stdin/stdout.
  /// </summary>
  public class IpcStreamOverJsonLines : IIpcStream {
    private readonly object _readerLock = new object();
    private readonly object _writerLock = new object();
    private readonly TextReader _reader;
    private readonly TextWriter _writer;

    public IpcStreamOverJsonLines(TextReader reader, TextWriter writer) {
      _reader = reader;
      _writer = writer;
    }

    public void WriteRequest(IpcRequest request) {
      WriteMessage(request);
    }

    public void WriteResponse(IpcResponse response) {
      WriteMessage(response);
    }

    public IpcResponse ReadResponse() {
      return ReadMessage<IpcResponse>();
    }

    public IpcRequest ReadRequest() {
      return ReadMessage<IpcRequest>();
    }

    private void WriteMessage(IpcMessage message) {
      // Serialize outside of the lock, a line never contains a new line character
      // (JSON strings escape them).
      var line = IpcJsonSerializer.Serialize(message);
      lock (_writerLock) {
        _writer.Write(line);
        _writer.Write('\n');
        _writer.Flush();
      }
    }

    /// <summary>
    /// Returns the next message, or <code>null</code> when the end of the input stream has been
    /// reached. Malformed lines are logged and skipped.
    /// </summary>
    private T ReadMessage<T>() where T : IpcMessage {
      lock (_readerLock) {
        while (true) {
          string line;
          try {
            line = _reader.ReadLine();
          }
          catch (Exception e) {
            Logger.LogError(e, "Error reading from JSON lines input stream");
            return null;
          }

          if (line == null) {
            Logger.LogInfo("JSON lines input stream was closed -- assuming normal termination.");
            return null;
          }

          if (string.IsNullOrWhiteSpace(line)) {
            continue;
          }

          try {
            var message = IpcJsonSerializer.Deserialize(line);
            var result = message as T;
            if (result == null) {
              Logger.LogError("Ignoring JSON message of unexpected type {0} (expected {1}): {2}",
                message?.GetType().Name, typeof(T).Name, line);
              continue;
            }
            return result;
          }
          catch (Exception e) {
            Logger.LogError(e, "Ignoring malformed JSON message: {0}", line);
          }
        }
      }
    }
  }
}
