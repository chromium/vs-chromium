// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.JsonRpc;

namespace VsChromium.Tests.JsonRpc {
  /// <summary>
  /// The framing of the JSON-RPC messages (LSP headers): <see cref="JsonRpcMessageReader"/> and <see
  /// cref="JsonRpcMessageWriter"/>.
  /// </summary>
  [TestClass]
  public class TestJsonRpcFraming {
    private static string Frame(string json) {
      return $"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}";
    }

    private static string[] ReadAll(string input, int chunkSize) {
      var reader = new JsonRpcMessageReader(new ChunkedStream(Encoding.UTF8.GetBytes(input), chunkSize), bufferSize: 16);
      var messages = new System.Collections.Generic.List<string>();
      byte[] message;
      while ((message = reader.ReadMessage()) != null) {
        messages.Add(Encoding.UTF8.GetString(message));
      }
      return messages.ToArray();
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(7)]
    [DataRow(100_000)]
    public void MessagesAreReadWhateverTheSizeOfTheReads(int chunkSize) {
      // Several messages in one read (large chunks), or split across reads (small chunks), with
      // non-ASCII text (the length is in bytes).
      var messages = new[] { "{\"id\":1}", "{\"text\":\"日本語 é 🎉\"}", "{}", new string('x', 5000) };
      var input = string.Concat(messages.Select(Frame));
      CollectionAssert.AreEqual(messages, ReadAll(input, chunkSize));
    }

    [TestMethod]
    public void HeaderNamesAreCaseInsensitiveAndOtherHeadersAreIgnored() {
      var input =
        "content-length: 2\r\n\r\n{}" +
        "Content-Type: application/vscode-jsonrpc; charset=utf-8\r\nCONTENT-LENGTH:3\r\n\r\n[1]" +
        "Content-Length: 4\r\nX-Other: value\r\n\r\n\"ab\"";
      CollectionAssert.AreEqual(new[] { "{}", "[1]", "\"ab\"" }, ReadAll(input, 5));
    }

    [TestMethod]
    public void EmptyStreamHasNoMessage() {
      CollectionAssert.AreEqual(new string[0], ReadAll("", 10));
    }

    [TestMethod]
    [DataRow("Content-Length 2\r\n\r\n{}", DisplayName = "No colon")]
    [DataRow("Content-Length: abc\r\n\r\n{}", DisplayName = "Invalid length")]
    [DataRow("Content-Length: -2\r\n\r\n{}", DisplayName = "Negative length")]
    [DataRow("Content-Type: text\r\n\r\n{}", DisplayName = "Missing Content-Length")]
    [DataRow("Content-Length: 10\r\n\r\n{}", DisplayName = "Truncated content")]
    [DataRow("Content-Length: 2\r\n", DisplayName = "Truncated headers")]
    [DataRow(": 2\r\n\r\n{}", DisplayName = "Empty header name")]
    public void InvalidFramingIsAnError(string input) {
      Assert.ThrowsExactly<JsonRpcFramingException>(() => ReadAll(input, 4));
    }

    [TestMethod]
    public void TooLongHeaderIsAnError() {
      Assert.ThrowsExactly<JsonRpcFramingException>(() => ReadAll("X-Header: " + new string('a', 20_000) + "\r\n", 1000));
    }

    [TestMethod]
    public void MessagesAreWrittenWithTheirHeader() {
      var stream = new MemoryStream();
      var writer = new JsonRpcMessageWriter(stream);
      writer.WriteMessage(Encoding.UTF8.GetBytes("{\"a\":\"é\"}"));
      using (var buffer = new JsonRpcMessageBuffer()) {
        // Large content: the buffer grows.
        var content = Encoding.UTF8.GetBytes("\"" + new string('z', 100_000) + "\"");
        content.AsSpan().CopyTo(buffer.GetSpan(content.Length));
        buffer.Advance(content.Length);
        writer.WriteMessage(buffer);
      }
      var text = Encoding.UTF8.GetString(stream.ToArray());
      Assert.AreEqual(Frame("{\"a\":\"é\"}") + Frame("\"" + new string('z', 100_000) + "\""), text);

      // And read back.
      CollectionAssert.AreEqual(new[] { "{\"a\":\"é\"}", "\"" + new string('z', 100_000) + "\"" }, ReadAll(text, 4096));
    }
  }
}
