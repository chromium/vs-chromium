// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Protocol.Generated;

namespace VsChromium.Tests.Protocol {
  /// <summary>
  /// The code generated from the protocol definition (src/Server/Protocol/Generated/Protocol.g.cs):
  /// dispatch of requests, JSON shapes, notifications, errors.
  /// </summary>
  [TestClass]
  public class TestProtocolDispatcher {
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    [TestMethod]
    public async Task RequestParametersAreDeserializedAndResultsSerialized() {
      var handler = new FakeHandler();
      var result = await ProtocolDispatcher.DispatchRequestAsync(handler, "search/code",
        Json("{\"query\":{\"text\":\"foo\",\"matchCase\":true},\"maxResults\":10}"), CancellationToken.None);

      Assert.AreEqual("foo", handler.LastSearch.Query.Text);
      Assert.IsTrue(handler.LastSearch.Query.MatchCase);
      Assert.IsNull(handler.LastSearch.Query.Regex);
      Assert.AreEqual(10, handler.LastSearch.MaxResults);
      Assert.AreEqual(
        "{\"projects\":[{\"rootPath\":\"C:\\\\src\",\"files\":[{\"relativePath\":\"a.cc\",\"matches\":[{\"position\":1,\"length\":3}]}]}],\"hitCount\":1,\"searchedFileCount\":2,\"totalFileCount\":3}",
        result.Value.GetRawText());
    }

    [TestMethod]
    public async Task InlineParametersAndVoidResultsWork() {
      var handler = new FakeHandler();
      var result = await ProtocolDispatcher.DispatchRequestAsync(handler, ProtocolMethods.FilesRegister,
        Json("{\"path\":\"C:\\\\src\\\\a.cc\"}"), CancellationToken.None);
      Assert.IsNull(result);
      Assert.AreEqual("C:\\src\\a.cc", handler.LastRegisteredPath);

      result = await ProtocolDispatcher.DispatchRequestAsync(handler, ProtocolMethods.TreeRefresh, null, CancellationToken.None);
      Assert.IsNull(result);
      Assert.IsTrue(handler.Refreshed);
    }

    [TestMethod]
    public async Task EnumsDatesAndOptionalValuesHaveTheProtocolJsonShapes() {
      var handler = new FakeHandler();
      var result = await ProtocolDispatcher.DispatchRequestAsync(handler, ProtocolMethods.IndexStatistics, Json("{}"), CancellationToken.None);
      var json = result.Value;
      Assert.AreEqual("busy", json.GetProperty("status").GetString());
      Assert.AreEqual("2026-11-02T15:00:00Z", json.GetProperty("indexLastUpdatedUtc").GetString());
      Assert.AreEqual(7_000_000_000L, json.GetProperty("serverNativeMemoryUsage").GetInt64());

      // Optional values are omitted when not set
      var error = JsonSerializer.SerializeToElement(new ErrorInfo { Message = "failed" }, ProtocolJsonContext.Default.ErrorInfo);
      Assert.AreEqual("{\"message\":\"failed\"}", error.GetRawText());
    }

    [TestMethod]
    public void Int64ValuesCanBeStrings() {
      // The protobuf JSON mapping writes 64 bit integers as strings.
      var value = JsonSerializer.Deserialize("{\"operationId\":\"12345678901\",\"treeVersion\":3}", ProtocolJsonContext.Default.FilesLoadedParams);
      Assert.AreEqual(12345678901L, value.OperationId);
      Assert.AreEqual(3L, value.TreeVersion);
    }

    [TestMethod]
    public async Task UnknownMethodsAndInvalidParametersAreProtocolErrors() {
      var handler = new FakeHandler();
      var notFound = await Assert.ThrowsExactlyAsync<ProtocolMethodNotFoundException>(() =>
        ProtocolDispatcher.DispatchRequestAsync(handler, "search/unknown", Json("{}"), CancellationToken.None).AsTask());
      Assert.AreEqual(-32601, notFound.Code);

      var invalid = await Assert.ThrowsExactlyAsync<ProtocolInvalidParamsException>(() =>
        ProtocolDispatcher.DispatchRequestAsync(handler, "search/code", Json("[1, 2]"), CancellationToken.None).AsTask());
      Assert.AreEqual(-32602, invalid.Code);
      await Assert.ThrowsExactlyAsync<ProtocolInvalidParamsException>(() =>
        ProtocolDispatcher.DispatchRequestAsync(handler, "search/code", Json("{\"maxResults\":\"many\"}"), CancellationToken.None).AsTask());
      await Assert.ThrowsExactlyAsync<ProtocolInvalidParamsException>(() =>
        ProtocolDispatcher.DispatchRequestAsync(handler, "search/code", null, CancellationToken.None).AsTask());

      Assert.IsTrue(ProtocolDispatcher.IsRequest("search/code"));
      Assert.IsFalse(ProtocolDispatcher.IsRequest("index/stateChanged"));
    }

    [TestMethod]
    public void NotificationsAreSerialized() {
      var sender = new FakeSender();
      sender.IndexStateChanged(new IndexStateChangedParams {
        Status = IndexingStatus.Yield,
        LastIndexUpdatedUtc = new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc),
      });
      Assert.AreEqual("index/stateChanged", sender.Method);
      Assert.AreEqual("{\"status\":\"yield\",\"lastIndexUpdatedUtc\":\"2026-11-02T15:00:00Z\"}", sender.Parameters.Value.GetRawText());
    }

    private sealed class FakeSender : ProtocolNotificationSender {
      public string Method;
      public JsonElement? Parameters;

      protected override void SendNotification(string method, JsonElement? parameters) {
        Method = method;
        Parameters = parameters;
      }
    }

    private sealed class FakeHandler : IProtocolRequestHandler {
      public SearchParams LastSearch;
      public string LastRegisteredPath;
      public bool Refreshed;

      public ValueTask<SearchCodeResult> SearchCodeAsync(SearchParams parameters, CancellationToken cancellationToken) {
        LastSearch = parameters;
        return ValueTask.FromResult(new SearchCodeResult {
          Projects = new List<ProjectMatches> {
            new ProjectMatches {
              RootPath = "C:\\src",
              Files = new List<FileMatches> {
                new FileMatches { RelativePath = "a.cc", Matches = new List<TextSpan> { new TextSpan { Position = 1, Length = 3 } } },
              },
            },
          },
          HitCount = 1,
          SearchedFileCount = 2,
          TotalFileCount = 3,
        });
      }

      public ValueTask FilesRegisterAsync(FilesRegisterParams parameters, CancellationToken cancellationToken) {
        LastRegisteredPath = parameters.Path;
        return ValueTask.CompletedTask;
      }

      public ValueTask TreeRefreshAsync(CancellationToken cancellationToken) {
        Refreshed = true;
        return ValueTask.CompletedTask;
      }

      public ValueTask<IndexStatistics> IndexStatisticsAsync(IndexStatisticsParams parameters, CancellationToken cancellationToken) {
        return ValueTask.FromResult(new IndexStatistics {
          Status = IndexingStatus.Busy,
          IndexLastUpdatedUtc = new DateTime(2026, 11, 2, 15, 0, 0, DateTimeKind.Utc),
          ServerNativeMemoryUsage = 7_000_000_000L,
        });
      }

      public ValueTask FilesUnregisterAsync(FilesUnregisterParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<FileExtractsResult> FilesExtractsAsync(FileExtractsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask IndexPauseAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask IndexResumeAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<IndexDetails> IndexDetailsAsync(DetailsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask ProjectSetOptionsAsync(SetProjectOptionsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<ProjectDetails> ProjectDetailsAsync(ProjectDetailsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<SearchPathsResult> SearchPathsAsync(SearchParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<InitializeResult> ServerInitializeAsync(InitializeParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<ServerStatistics> ServerStatisticsAsync(ServerStatisticsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<ServerHistory> ServerHistoryAsync(ServerHistoryParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<IndexStatus> IndexStatusAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<ClearCacheResult> IndexClearCacheAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<ClearCacheResult> ProjectClearCacheAsync(ProjectPathParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask ProjectRebuildAsync(ProjectPathParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<FileSystemTree> TreeGetAsync(TreeParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<DirectoryEntries> TreeEntriesAsync(TreeEntriesParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<DirectoryEntriesList> TreeEntriesMultipleAsync(TreeEntriesMultipleParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
      public ValueTask<DirectoryDetails> TreeDirectoryDetailsAsync(DirectoryDetailsParams parameters, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
  }
}
