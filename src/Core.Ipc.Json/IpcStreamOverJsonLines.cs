// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;

namespace VsChromium.Core.Ipc.Json {
  /// <summary>
  /// <see cref="IIpcStream"/> implementation exchanging JSON messages (see <see
  /// cref="IpcJsonSerializer"/>), one message per line ("JSON lines"), over the standard
  /// input/output of the server (<c>--stdio</c> command line argument). Used by both the server and the
  /// Visual Studio package.
  /// </summary>
  public class IpcStreamOverJsonLines : IIpcStream {
    private readonly object _readerLock = new object();
    private readonly object _writerLock = new object();
    private readonly TextReader _reader;
    private readonly TextWriter _writer;
    private readonly Action<Exception, string> _logError;
    private readonly Action<string> _logInfo;

    /// <param name="reader">The input stream.</param>
    /// <param name="writer">The output stream.</param>
    /// <param name="logError">Optional: logging of errors (this assembly has no logging
    /// dependency).</param>
    /// <param name="logInfo">Optional: logging of informational messages.</param>
    public IpcStreamOverJsonLines(TextReader reader, TextWriter writer,
                                  Action<Exception, string> logError = null, Action<string> logInfo = null) {
      _reader = reader;
      _writer = writer;
      _logError = logError ?? ((e, message) => { });
      _logInfo = logInfo ?? (message => { });
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
            _logError(e, "Error reading from JSON lines input stream");
            return null;
          }

          if (line == null) {
            _logInfo("JSON lines input stream was closed -- assuming normal termination.");
            return null;
          }

          if (string.IsNullOrWhiteSpace(line)) {
            continue;
          }

          try {
            var message = IpcJsonSerializer.Deserialize(line);
            var result = message as T;
            if (result == null) {
              _logError(null, string.Format("Ignoring JSON message of unexpected type {0} (expected {1}): {2}",
                message?.GetType().Name, typeof(T).Name, line));
              continue;
            }
            return result;
          }
          catch (Exception e) {
            _logError(e, string.Format("Ignoring malformed JSON message: {0}", line));
          }
        }
      }
    }
  }
}
