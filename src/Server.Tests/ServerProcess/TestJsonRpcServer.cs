// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.JsonRpc;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  /// <summary>
  /// End-to-end tests of the "--jsonrpc" mode of VsChromium.Server.exe (JSON-RPC 2.0 with LSP
  /// framing over stdin/stdout), on the test enlistment.
  /// </summary>
  [TestClass]
  public class TestJsonRpcServer {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    [TestMethod]
    public void InitializeRegisterAndRequestsWork() {
      using (var server = new JsonRpcServerProcess()) {
        var initialize = server.Request("server/initialize", "{\"clientName\":\"test\",\"protocolVersion\":1}");
        Assert.AreEqual(1, initialize.GetProperty("protocolVersion").GetInt32());
        Assert.AreEqual(server.Process.Id, initialize.GetProperty("serverProcessId").GetInt32());
        Assert.IsFalse(string.IsNullOrEmpty(initialize.GetProperty("serverVersion").GetString()));

        // Register a file of the test enlistment and wait for the index to be ready.
        var testFile = Utils.GetChromiumTestEnlistmentFile();
        var root = testFile.DirectoryName;
        var registerId = server.Send("files/register", JsonSerializer.Serialize(new { path = testFile.FullName }));
        Assert.AreEqual(JsonValueKind.Null, server.WaitForResponse(registerId).ValueKind);
        server.WaitForNotification("index/loaded");

        // Code search: flat results, grouped by project.
        var search = server.Request("search/code", "{\"query\":{\"text\":\"Test directory looking like\",\"matchCase\":true},\"maxResults\":100}");
        Assert.AreEqual(1, search.GetProperty("hitCount").GetInt32());
        var project = search.GetProperty("projects").EnumerateArray().Single();
        Assert.AreEqual(root, project.GetProperty("rootPath").GetString());
        var file = project.GetProperty("files").EnumerateArray().Single();
        var relativePath = file.GetProperty("relativePath").GetString();
        var match = file.GetProperty("matches").EnumerateArray().Single();
        Assert.AreEqual("Test directory looking like".Length, match.GetProperty("length").GetInt32());

        // Extracts of the match.
        var extracts = server.Request("files/extracts", JsonSerializer.Serialize(new {
          path = Path.Combine(root, relativePath),
          spans = new[] { new { position = match.GetProperty("position").GetInt32(), length = match.GetProperty("length").GetInt32() } },
          maxExtractLength = 100,
        }));
        StringAssert.Contains(extracts.GetProperty("extracts").EnumerateArray().Single().GetProperty("text").GetString(), "Test directory looking like");

        // Path search.
        var paths = server.Request("search/paths", "{\"query\":{\"text\":\"" + testFile.Name + "\"},\"maxResults\":100}");
        Assert.IsGreaterThanOrEqualTo(1, paths.GetProperty("hitCount").GetInt32());
        Assert.AreEqual(root, paths.GetProperty("projects").EnumerateArray().First().GetProperty("rootPath").GetString());

        // Tree: the known version gives no projects.
        var tree = server.Request("tree/get", "{}");
        Assert.AreEqual(root, tree.GetProperty("projects").EnumerateArray().Single().GetProperty("rootPath").GetString());
        var version = tree.GetProperty("version").GetInt32();
        tree = server.Request("tree/get", "{\"knownVersion\":" + version + "}");
        Assert.AreEqual(0, tree.GetProperty("projects").GetArrayLength());

        var entries = server.Request("tree/entriesMultiple", JsonSerializer.Serialize(new { projectPath = root, relativePaths = new[] { "", "does-not-exist" } }));
        var directories = entries.GetProperty("directories").EnumerateArray().ToList();
        Assert.IsTrue(directories[0].GetProperty("exists").GetBoolean());
        CollectionAssert.Contains(directories[0].GetProperty("fileNames").EnumerateArray().Select(x => x.GetString()).ToList(), testFile.Name);
        Assert.IsFalse(directories[1].GetProperty("exists").GetBoolean());
        Assert.AreEqual("does-not-exist", directories[1].GetProperty("relativePath").GetString());

        // Statistics and details.
        Assert.AreEqual(1, server.Request("index/statistics", "{}").GetProperty("projectCount").GetInt32());
        var details = server.Request("project/details", JsonSerializer.Serialize(new { projectPath = root, maxFilesByExtensionDetailsCount = 5, maxLargeFilesDetailsCount = 5 }));
        Assert.AreEqual(root, details.GetProperty("rootPath").GetString());
        Assert.IsGreaterThan(0, details.GetProperty("directoryDetails").GetProperty("fileCount").GetInt32());
        var statistics = server.Request("server/statistics", "{}");
        Assert.IsTrue(statistics.GetProperty("operationTimings").EnumerateArray().Any(x => x.GetProperty("name").GetString() == "Request:search/code"));

        // Errors.
        var error = server.RequestError("project/details", JsonSerializer.Serialize(new { projectPath = Path.Combine(root, "foo"), maxFilesByExtensionDetailsCount = 0, maxLargeFilesDetailsCount = 0 }));
        Assert.AreEqual(JsonRpcErrorCodes.RecoverableError, error.GetProperty("code").GetInt32());
        error = server.RequestError("search/code", "{\"query\":{\"text\":\"(\",\"regex\":true},\"maxResults\":100}");
        Assert.AreEqual(JsonRpcErrorCodes.RecoverableError, error.GetProperty("code").GetInt32());
      }
    }

    [TestMethod]
    public void ServerExitsWhenStdinIsClosed() {
      using (var server = new JsonRpcServerProcess()) {
        server.Request("server/initialize", "{\"protocolVersion\":1}");
        server.CloseInput();
        Assert.IsTrue(server.Process.WaitForExit((int)Timeout.TotalMilliseconds),
          "Server process should exit when stdin is closed.");
        Assert.AreEqual(0, server.Process.ExitCode);
      }
    }

    private class JsonRpcServerProcess : IDisposable {
      private readonly BlockingCollection<JsonElement> _messages = new BlockingCollection<JsonElement>();
      private readonly Thread _readerThread;
      private int _nextId;

      public JsonRpcServerProcess() {
        var serverPath = Path.Combine(
          Path.GetDirectoryName(typeof(TestJsonRpcServer).Assembly.Location),
          "VsChromium.Server.exe");
        Assert.IsTrue(File.Exists(serverPath), "Server executable not found: " + serverPath);

        Process = Process.Start(new ProcessStartInfo {
          FileName = serverPath,
          Arguments = "--jsonrpc",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardInput = true,
          RedirectStandardOutput = true,
        });

        _readerThread = new Thread(() => {
          try {
            var reader = new JsonRpcMessageReader(Process.StandardOutput.BaseStream);
            byte[] message;
            while ((message = reader.ReadMessage()) != null) {
              _messages.Add(JsonDocument.Parse(message).RootElement.Clone());
            }
          }
          finally {
            _messages.CompleteAdding();
          }
        }) { IsBackground = true };
        _readerThread.Start();
      }

      public Process Process { get; }

      public int Send(string method, string parameters) {
        var id = Interlocked.Increment(ref _nextId);
        var json = $"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{parameters}}}";
        var bytes = Encoding.UTF8.GetBytes($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
        Process.StandardInput.BaseStream.Write(bytes, 0, bytes.Length);
        Process.StandardInput.BaseStream.Flush();
        return id;
      }

      public JsonElement Request(string method, string parameters) {
        return WaitForResponse(Send(method, parameters));
      }

      public JsonElement RequestError(string method, string parameters) {
        var id = Send(method, parameters);
        var response = WaitForMessage(m => m.TryGetProperty("id", out var value) && value.GetInt32() == id);
        Assert.IsFalse(response.TryGetProperty("result", out _), response.ToString());
        return response.GetProperty("error");
      }

      public void CloseInput() {
        Process.StandardInput.Close();
      }

      /// <summary>Returns the result of request <paramref name="id"/> (which must succeed).</summary>
      public JsonElement WaitForResponse(int id) {
        var response = WaitForMessage(m => m.TryGetProperty("id", out var value) && value.GetInt32() == id);
        Assert.IsFalse(response.TryGetProperty("error", out var error), "Server returned an error: " + error.ToString());
        return response.GetProperty("result");
      }

      public JsonElement WaitForNotification(string method) {
        return WaitForMessage(m => !m.TryGetProperty("id", out _) && m.GetProperty("method").GetString() == method);
      }

      /// <summary>
      /// Returns the first message matching <paramref name="predicate"/>, skipping other messages
      /// (e.g. notifications).
      /// </summary>
      private JsonElement WaitForMessage(Func<JsonElement, bool> predicate) {
        var deadline = DateTime.UtcNow + Timeout;
        while (true) {
          var remaining = deadline - DateTime.UtcNow;
          JsonElement message = default;
          if (remaining <= TimeSpan.Zero || !_messages.TryTake(out message, remaining)) {
            Assert.Fail("Timeout waiting for a message from the server.");
          }
          if (predicate(message)) {
            return message;
          }
        }
      }

      public void Dispose() {
        try {
          if (!Process.HasExited) {
            Process.StandardInput.Close();
            if (!Process.WaitForExit(5000)) {
              Process.Kill();
            }
          }
        }
        catch (InvalidOperationException) {
          // Input stream already closed.
        }
        _readerThread.Join(TimeSpan.FromSeconds(5));
        Process.Dispose();
      }
    }
  }
}
