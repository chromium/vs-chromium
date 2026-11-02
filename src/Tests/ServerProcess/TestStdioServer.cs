// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Ipc;
using VsChromium.Core.Ipc.TypedMessages;
using VsChromium.Server.Ipc.Json;
using VsChromium.Tests.Server;

namespace VsChromium.Tests.ServerProcess {
  /// <summary>
  /// End-to-end tests of the "--stdio" mode of VsChromium.Server.exe (JSON lines over
  /// stdin/stdout), as used by the VS Code extension.
  /// </summary>
  [TestClass]
  public class TestStdioServer {
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [TestMethod]
    public void HelloRegisterAndSearchWork() {
      using (var server = new StdioServer()) {
        // The server always starts by sending the "hello" response.
        var hello = server.WaitForMessage<IpcResponse>(m => m.Protocol == IpcProtocols.Hello);
        Assert.AreEqual("Hello!", ((IpcStringData)hello.Data).Text);

        // Register a file of the test enlistment and wait for the index to be ready.
        var testFile = Utils.GetChromiumTestEnlistmentFile();
        server.Send(new IpcRequest {
          RequestId = 10,
          Protocol = IpcProtocols.TypedMessage,
          Data = new RegisterFileRequest { FileName = testFile.FullName }
        });
        Assert.IsInstanceOfType(server.WaitForResponse(10).Data, typeof(DoneResponse));
        server.WaitForMessage<IpcEvent>(m => m.Data is SearchEngineFilesLoaded);

        // Search.
        server.Send(new IpcRequest {
          RequestId = 11,
          Protocol = IpcProtocols.TypedMessage,
          Data = new SearchCodeRequest {
            SearchParams = new SearchParams {
              SearchString = "Test directory looking like",
              MaxResults = 100,
              MatchCase = true,
            }
          }
        });
        var searchResponse = server.WaitForResponse(11).Data as SearchCodeResponse;
        Assert.IsNotNull(searchResponse);
        Assert.AreEqual(1, searchResponse.HitCount);
        Assert.AreEqual(testFile.DirectoryName, searchResponse.SearchResults.Entries.Single().Name);
      }
    }

    [TestMethod]
    public void ServerExitsWhenStdinIsClosed() {
      using (var server = new StdioServer()) {
        server.WaitForMessage<IpcResponse>(m => m.Protocol == IpcProtocols.Hello);
        server.CloseInput();
        Assert.IsTrue(server.Process.WaitForExit((int)Timeout.TotalMilliseconds),
          "Server process should exit when stdin is closed.");
        Assert.AreEqual(0, server.Process.ExitCode);
      }
    }

    private class StdioServer : IDisposable {
      private readonly BlockingCollection<IpcMessage> _messages = new BlockingCollection<IpcMessage>();
      private readonly Thread _readerThread;

      public StdioServer() {
        var serverPath = Path.Combine(
          Path.GetDirectoryName(typeof(TestStdioServer).Assembly.Location),
          "VsChromium.Server.exe");
        Assert.IsTrue(File.Exists(serverPath), "Server executable not found: " + serverPath);

        Process = Process.Start(new ProcessStartInfo {
          FileName = serverPath,
          Arguments = "--stdio",
          UseShellExecute = false,
          CreateNoWindow = true,
          RedirectStandardInput = true,
          RedirectStandardOutput = true,
          StandardOutputEncoding = new UTF8Encoding(false),
        });

        _readerThread = new Thread(() => {
          string line;
          while ((line = Process.StandardOutput.ReadLine()) != null) {
            _messages.Add(IpcJsonSerializer.Deserialize(line));
          }
          _messages.CompleteAdding();
        }) { IsBackground = true };
        _readerThread.Start();
      }

      public Process Process { get; }

      public void Send(IpcRequest request) {
        Process.StandardInput.Write(IpcJsonSerializer.Serialize(request) + "\n");
        Process.StandardInput.Flush();
      }

      public void CloseInput() {
        Process.StandardInput.Close();
      }

      public IpcResponse WaitForResponse(long requestId) {
        var response = WaitForMessage<IpcResponse>(m => !(m is IpcEvent) && m.RequestId == requestId);
        Assert.IsNotInstanceOfType(response.Data, typeof(ErrorResponse),
          "Server returned an error: " + (response.Data as ErrorResponse)?.Message);
        return response;
      }

      /// <summary>
      /// Returns the first message of type <typeparamref name="T"/> matching <paramref
      /// name="predicate"/>, skipping other messages (e.g. progress events).
      /// </summary>
      public T WaitForMessage<T>(Func<T, bool> predicate) where T : IpcMessage {
        var deadline = DateTime.UtcNow + Timeout;
        while (true) {
          var remaining = deadline - DateTime.UtcNow;
          IpcMessage message = null;
          if (remaining <= TimeSpan.Zero || !_messages.TryTake(out message, remaining)) {
            Assert.Fail("Timeout waiting for message of type {0} from server.", typeof(T).Name);
          }
          var typed = message as T;
          if (typed != null && predicate(typed)) {
            return typed;
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
