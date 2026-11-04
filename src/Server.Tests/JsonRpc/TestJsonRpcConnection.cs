// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Utility;
using VsChromium.Protocol.Generated;
using VsChromium.Server.JsonRpc;

namespace VsChromium.Tests.JsonRpc {
  /// <summary>
  /// The JSON-RPC 2.0 connection of the server (<see cref="JsonRpcConnection"/>) over in-memory
  /// streams, with a fake request handler: responses, errors, cancellation, notifications,
  /// sequential requests.
  /// </summary>
  [TestClass]
  public class TestJsonRpcConnection {
    [TestMethod]
    public void RequestWithNumberIdReturnsTheResult() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"server/initialize\",\"params\":{\"clientName\":\"test\",\"protocolVersion\":1}}");
        var response = test.Receive();
        Assert.AreEqual("2.0", response.GetProperty("jsonrpc").GetString());
        Assert.AreEqual(JsonValueKind.Number, response.GetProperty("id").ValueKind);
        Assert.AreEqual(1, response.GetProperty("id").GetInt32());
        Assert.AreEqual("1.2.3", response.GetProperty("result").GetProperty("serverVersion").GetString());
        Assert.IsFalse(response.TryGetProperty("error", out _));
      }
    }

    [TestMethod]
    public void RequestIdIsEchoedAsIs() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":\"abc-1\",\"method\":\"server/initialize\",\"params\":{\"protocolVersion\":1}}");
        var response = test.Receive();
        Assert.AreEqual(JsonValueKind.String, response.GetProperty("id").ValueKind);
        Assert.AreEqual("abc-1", response.GetProperty("id").GetString());

        test.Send("{\"jsonrpc\":\"2.0\",\"id\":12345678901,\"method\":\"server/initialize\",\"params\":{\"protocolVersion\":1}}");
        Assert.AreEqual(12345678901L, test.Receive().GetProperty("id").GetInt64());
      }
    }

    [TestMethod]
    public void VoidResultIsNull() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":2,\"method\":\"files/register\",\"params\":{\"path\":\"C:\\\\src\\\\a.cc\"}}");
        var response = test.Receive();
        Assert.AreEqual(JsonValueKind.Null, response.GetProperty("result").ValueKind);
        Assert.AreEqual("C:\\src\\a.cc", test.Handler.RegisteredPath);

        // No parameters
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":3,\"method\":\"tree/refresh\"}");
        Assert.AreEqual(JsonValueKind.Null, test.Receive().GetProperty("result").ValueKind);
      }
    }

    [TestMethod]
    public void UnknownMethodIsMethodNotFound() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":4,\"method\":\"foo/bar\",\"params\":{}}");
        var error = test.ReceiveError(4);
        Assert.AreEqual(JsonRpcErrorCodes.MethodNotFound, error.GetProperty("code").GetInt32());
        StringAssert.Contains(error.GetProperty("message").GetString(), "foo/bar");
      }
    }

    [TestMethod]
    [DataRow("[1, 2]", DisplayName = "Array")]
    [DataRow("{\"path\": 12}", DisplayName = "Wrong type")]
    public void InvalidParametersAreInvalidParams(string parameters) {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"files/register\",\"params\":" + parameters + "}");
        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, test.ReceiveError(5).GetProperty("code").GetInt32());
      }
    }

    [TestMethod]
    public void MissingParametersAreInvalidParams() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":5,\"method\":\"files/register\"}");
        Assert.AreEqual(JsonRpcErrorCodes.InvalidParams, test.ReceiveError(5).GetProperty("code").GetInt32());
      }
    }

    [TestMethod]
    public void RecoverableErrorHasItsMessage() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":6,\"method\":\"project/setOptions\",\"params\":{\"projectPath\":\"relative\"}}");
        var error = test.ReceiveError(6);
        Assert.AreEqual(JsonRpcErrorCodes.RecoverableError, error.GetProperty("code").GetInt32());
        Assert.AreEqual("Invalid project path \"relative\"", error.GetProperty("message").GetString());
        Assert.IsFalse(error.TryGetProperty("data", out _));
      }
    }

    [TestMethod]
    public void UnexpectedErrorIsInternalErrorWithDetails() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"project/details\",\"params\":{\"projectPath\":\"C:\\\\src\"}}");
        var error = test.ReceiveError(7);
        Assert.AreEqual(JsonRpcErrorCodes.InternalError, error.GetProperty("code").GetInt32());
        Assert.AreEqual("boom", error.GetProperty("message").GetString());
        var data = error.GetProperty("data");
        Assert.AreEqual("boom", data.GetProperty("message").GetString());
        Assert.AreEqual(typeof(InvalidOperationException).FullName, data.GetProperty("typeName").GetString());
        StringAssert.Contains(data.GetProperty("stackTrace").GetString(), nameof(FakeHandler));
        Assert.AreEqual("inner boom", data.GetProperty("inner").GetProperty("message").GetString());
      }
    }

    [TestMethod]
    [DataRow("{not json", JsonRpcErrorCodes.ParseError, DisplayName = "Invalid JSON")]
    [DataRow("[{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tree/refresh\"}]", JsonRpcErrorCodes.InvalidRequest, DisplayName = "Batch")]
    [DataRow("{\"jsonrpc\":\"2.0\",\"id\":{},\"method\":\"tree/refresh\"}", JsonRpcErrorCodes.InvalidRequest, DisplayName = "Invalid id")]
    public void InvalidMessagesAreErrorsWithNullId(string message, int code) {
      using (var test = new ConnectionTest()) {
        test.Send(message);
        var response = test.Receive();
        Assert.AreEqual(JsonValueKind.Null, response.GetProperty("id").ValueKind);
        Assert.AreEqual(code, response.GetProperty("error").GetProperty("code").GetInt32());

        // The connection still works.
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":8,\"method\":\"tree/refresh\"}");
        Assert.AreEqual(8, test.Receive().GetProperty("id").GetInt32());
      }
    }

    [TestMethod]
    [DataRow("{\"jsonrpc\":\"1.0\",\"id\":9,\"method\":\"tree/refresh\"}", DisplayName = "Wrong version")]
    [DataRow("{\"id\":9,\"method\":\"tree/refresh\"}", DisplayName = "No version")]
    [DataRow("{\"jsonrpc\":\"2.0\",\"id\":9}", DisplayName = "No method")]
    [DataRow("{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":1}", DisplayName = "Method not a string")]
    [DataRow("{\"jsonrpc\":\"2.0\",\"id\":9,\"method\":\"tree/refresh\",\"params\":3}", DisplayName = "Params not structured")]
    public void InvalidRequestsAreInvalidRequestErrors(string message) {
      using (var test = new ConnectionTest()) {
        test.Send(message);
        Assert.AreEqual(JsonRpcErrorCodes.InvalidRequest, test.ReceiveError(9).GetProperty("code").GetInt32());
      }
    }

    [TestMethod]
    public void CancelRequestCancelsTheRequest() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":10,\"method\":\"search/code\",\"params\":{\"query\":{\"text\":\"foo\"},\"maxResults\":10}}");
        Assert.IsTrue(test.Handler.SearchStarted.Wait(TimeSpan.FromSeconds(10)));
        // Unknown ids and ids of another type are ignored.
        test.Send("{\"jsonrpc\":\"2.0\",\"method\":\"$/cancelRequest\",\"params\":{\"id\":\"10\"}}");
        test.Send("{\"jsonrpc\":\"2.0\",\"method\":\"$/cancelRequest\",\"params\":{\"id\":11}}");
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":12,\"method\":\"tree/refresh\"}");
        Assert.AreEqual(12, test.Receive().GetProperty("id").GetInt32());

        test.Send("{\"jsonrpc\":\"2.0\",\"method\":\"$/cancelRequest\",\"params\":{\"id\":10}}");
        var error = test.ReceiveError(10);
        Assert.AreEqual(JsonRpcErrorCodes.RequestCancelled, error.GetProperty("code").GetInt32());
      }
    }

    [TestMethod]
    public void UnknownNotificationsAreIgnored() {
      using (var test = new ConnectionTest()) {
        test.Send("{\"jsonrpc\":\"2.0\",\"method\":\"foo/bar\",\"params\":{}}");
        test.Send("{\"jsonrpc\":\"2.0\",\"method\":\"$/setTrace\",\"params\":{\"value\":\"off\"}}");
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":13,\"method\":\"tree/refresh\"}");
        // The first message is the response to the request (no response to notifications).
        Assert.AreEqual(13, test.Receive().GetProperty("id").GetInt32());
      }
    }

    [TestMethod]
    public void NotificationsAreWritten() {
      using (var test = new ConnectionTest()) {
        var sender = new JsonRpcNotificationSender(test.JsonRpcOutput);
        sender.IndexStateChanged(new IndexStateChangedParams {
          Status = IndexingStatus.Busy,
          LastIndexUpdatedUtc = new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc),
        });
        sender.SendProgress("Loading files", 3, 10);

        var notification = test.Receive();
        Assert.AreEqual("2.0", notification.GetProperty("jsonrpc").GetString());
        Assert.AreEqual("index/stateChanged", notification.GetProperty("method").GetString());
        Assert.IsFalse(notification.TryGetProperty("id", out _));
        Assert.AreEqual("busy", notification.GetProperty("params").GetProperty("status").GetString());
        Assert.AreEqual("2026-11-02T15:00:00Z", notification.GetProperty("params").GetProperty("lastIndexUpdatedUtc").GetString());

        notification = test.Receive();
        Assert.AreEqual("index/progress", notification.GetProperty("method").GetString());
        Assert.AreEqual("Loading files", notification.GetProperty("params").GetProperty("displayText").GetString());
        Assert.AreEqual(3, notification.GetProperty("params").GetProperty("completed").GetInt32());
        Assert.AreEqual(10, notification.GetProperty("params").GetProperty("total").GetInt32());
      }
    }

    [TestMethod]
    public void PauseAndResumeRunInOrder() {
      using (var test = new ConnectionTest()) {
        // "index/pause" is slow: "index/resume" must wait for it, other requests don't.
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":20,\"method\":\"index/pause\"}");
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":21,\"method\":\"index/resume\"}");
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":22,\"method\":\"index/pause\"}");
        test.Send("{\"jsonrpc\":\"2.0\",\"id\":23,\"method\":\"tree/refresh\"}");
        Assert.AreEqual(23, test.Receive().GetProperty("id").GetInt32());
        Assert.AreEqual(20, test.Receive().GetProperty("id").GetInt32());
        Assert.AreEqual(21, test.Receive().GetProperty("id").GetInt32());
        Assert.AreEqual(22, test.Receive().GetProperty("id").GetInt32());
        Assert.AreEqual("pause-start,pause-end,resume,pause-start,pause-end", string.Join(",", test.Handler.Events));
      }
    }

    [TestMethod]
    public void EndOfInputEndsTheConnection() {
      var test = new ConnectionTest();
      test.Send("{\"jsonrpc\":\"2.0\",\"id\":30,\"method\":\"tree/refresh\"}");
      Assert.AreEqual(30, test.Receive().GetProperty("id").GetInt32());
      test.Input.CompleteWriting();
      Assert.IsTrue(test.Thread.Join(TimeSpan.FromSeconds(10)));
      test.Dispose();
    }

    [TestMethod]
    public void FramingErrorEndsTheConnection() {
      var test = new ConnectionTest();
      test.SendRaw("Content-Length: x\r\n\r\n{}");
      var response = test.Receive();
      Assert.AreEqual(JsonRpcErrorCodes.ParseError, response.GetProperty("error").GetProperty("code").GetInt32());
      Assert.IsTrue(test.Thread.Join(TimeSpan.FromSeconds(10)));
      test.Dispose();
    }

    /// <summary>A connection running on its own thread, with a <see cref="FakeHandler"/>.</summary>
    private sealed class ConnectionTest : IDisposable {
      private readonly JsonRpcMessageReader _outputReader;

      public ConnectionTest() {
        JsonRpcOutput.Connect(Output);
        Connection = new JsonRpcConnection(Handler, JsonRpcOutput);
        _outputReader = new JsonRpcMessageReader(Output);
        Thread = new Thread(() => Connection.Run(Input)) { IsBackground = true, Name = "JSON-RPC connection test" };
        Thread.Start();
      }

      public FakeHandler Handler { get; } = new FakeHandler();
      public JsonRpcOutput JsonRpcOutput { get; } = new JsonRpcOutput();
      public JsonRpcConnection Connection { get; }
      public PipeStream Input { get; } = new PipeStream();
      public PipeStream Output { get; } = new PipeStream { ReadTimeout = 10_000 };
      public Thread Thread { get; }

      public void Send(string json) {
        SendRaw($"Content-Length: {Encoding.UTF8.GetByteCount(json)}\r\n\r\n{json}");
      }

      public void SendRaw(string text) {
        var bytes = Encoding.UTF8.GetBytes(text);
        Input.Write(bytes, 0, bytes.Length);
      }

      public JsonElement Receive() {
        var message = _outputReader.ReadMessage();
        Assert.IsNotNull(message, "End of the output stream");
        return JsonDocument.Parse(message).RootElement.Clone();
      }

      /// <summary>Receives the response of request <paramref name="id"/>, which must be an error.</summary>
      public JsonElement ReceiveError(int id) {
        var response = Receive();
        Assert.AreEqual(id, response.GetProperty("id").GetInt32());
        Assert.IsFalse(response.TryGetProperty("result", out _), response.GetRawText());
        return response.GetProperty("error");
      }

      public void Dispose() {
        Input.CompleteWriting();
        Thread.Join(TimeSpan.FromSeconds(10));
      }
    }

    private sealed class FakeHandler : IProtocolRequestHandler {
      public string RegisteredPath { get; private set; }
      public ManualResetEventSlim SearchStarted { get; } = new ManualResetEventSlim();
      public ConcurrentQueue<string> Events { get; } = new ConcurrentQueue<string>();

      public ValueTask<InitializeResult> ServerInitializeAsync(InitializeParams parameters, CancellationToken cancellationToken) {
        return new ValueTask<InitializeResult>(new InitializeResult { ServerVersion = "1.2.3", ProtocolVersion = 1, ServerProcessId = 42 });
      }

      public ValueTask FilesRegisterAsync(FilesRegisterParams parameters, CancellationToken cancellationToken) {
        RegisteredPath = parameters.Path;
        return default;
      }

      public ValueTask TreeRefreshAsync(CancellationToken cancellationToken) {
        return default;
      }

      public ValueTask ProjectSetOptionsAsync(SetProjectOptionsParams parameters, CancellationToken cancellationToken) {
        throw new RecoverableErrorException($"Invalid project path \"{parameters.ProjectPath}\"");
      }

      public ValueTask<ProjectDetails> ProjectDetailsAsync(ProjectDetailsParams parameters, CancellationToken cancellationToken) {
        throw new InvalidOperationException("boom", new ArgumentException("inner boom"));
      }

      public async ValueTask<SearchCodeResult> SearchCodeAsync(SearchParams parameters, CancellationToken cancellationToken) {
        SearchStarted.Set();
        await Task.Delay(Timeout.Infinite, cancellationToken);
        return new SearchCodeResult();
      }

      public ValueTask IndexPauseAsync(CancellationToken cancellationToken) {
        Events.Enqueue("pause-start");
        Thread.Sleep(300);
        Events.Enqueue("pause-end");
        return default;
      }

      public ValueTask IndexResumeAsync(CancellationToken cancellationToken) {
        Events.Enqueue("resume");
        return default;
      }

      public ValueTask FilesUnregisterAsync(FilesUnregisterParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<FileExtractsResult> FilesExtractsAsync(FileExtractsParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<IndexStatistics> IndexStatisticsAsync(IndexStatisticsParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<IndexDetails> IndexDetailsAsync(DetailsParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<SearchPathsResult> SearchPathsAsync(SearchParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<ServerStatistics> ServerStatisticsAsync(ServerStatisticsParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<ServerHistory> ServerHistoryAsync(ServerHistoryParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<IndexStatus> IndexStatusAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<ClearCacheResult> IndexClearCacheAsync(ClearCacheParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<FileSystemTree> TreeGetAsync(TreeParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<DirectoryEntries> TreeEntriesAsync(TreeEntriesParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<DirectoryEntriesList> TreeEntriesMultipleAsync(TreeEntriesMultipleParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
      public ValueTask<DirectoryDetails> TreeDirectoryDetailsAsync(DirectoryDetailsParams parameters, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
  }
}
