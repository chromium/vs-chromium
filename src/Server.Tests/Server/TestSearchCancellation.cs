// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Threads;
using VsChromium.Protocol.Generated;
using VsChromium.Server;
using VsChromium.Server.FileSystemNames;
using VsChromium.Server.JsonRpc;
using VsChromium.Server.Search;
using VsChromium.Tests.JsonRpc;
using EngineSearchParams = VsChromium.Server.Search.SearchParams;
using ProtocolSearchParams = VsChromium.Protocol.Generated.SearchParams;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestSearchCancellation : ServerServicesTestBase {
    [TestMethod]
    public async Task PreCancelledTokenThrowsOperationCanceledForCodeAndPathSearches() {
      using (var container = CreateServerServices()) {
        var loaded = new ManualResetEventSlim(false);
        container.SearchEngine.FilesLoaded += (_, _) => loaded.Set();
        container.FileRegistrationTracker.RegisterFileAsync(new FullPath(Utils.GetChromiumTestEnlistmentFile().FullName));
        Assert.IsTrue(loaded.Wait(TimeSpan.FromSeconds(15)));

        using (var cts = new CancellationTokenSource()) {
          cts.Cancel();

          Assert.Throws<OperationCanceledException>(() =>
            container.SearchEngine.SearchCode(new EngineSearchParams {
              SearchString = "Test directory",
              MaxResults = 100,
            }, cts.Token));

          Assert.Throws<OperationCanceledException>(() =>
            container.SearchEngine.SearchFilePaths(new EngineSearchParams {
              SearchString = "*.txt",
              MaxResults = 100,
            }, cts.Token));

          await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await container.ProtocolRequestHandler.SearchCodeAsync(new ProtocolSearchParams {
              Query = new SearchQuery { Text = "Test directory" },
              MaxResults = 100,
            }, cts.Token));

          await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await container.ProtocolRequestHandler.SearchPathsAsync(new ProtocolSearchParams {
              Query = new SearchQuery { Text = "*.txt" },
              MaxResults = 100,
            }, cts.Token));
        }

        // Subsequent non-cancelled searches succeed normally.
        var ok = container.SearchEngine.SearchCode(new EngineSearchParams {
          SearchString = "Test directory looking like",
          MaxResults = 100,
          MatchCase = true,
        });
        Assert.AreEqual(1, ok.HitCount);
      }
    }

    [TestMethod]
    public void MidSearchCancellationAbortsAndThrowsWithoutReturningPartialResults() {
      using (var container = CreateServerServices()) {
        Action onFileVisited = null;
        var interceptingFactory = new InterceptingSearchDataFactory(
          container.CompiledTextSearchDataFactory,
          () => onFileVisited?.Invoke());

        var searchEngine = new SearchEngine(
          container.FileSystemSnapshotManager,
          container.FileSystemNameFactory,
          container.LongRunningFileSystemTaskQueue,
          container.FileDatabaseSnapshotFactory,
          container.ProjectDiscovery,
          interceptingFactory,
          container.OperationProcessor,
          container.IndexingWorkerPool);

        var loaded = new ManualResetEventSlim(false);
        searchEngine.FilesLoaded += (_, _) => loaded.Set();
        container.FileRegistrationTracker.RegisterFileAsync(new FullPath(Utils.GetChromiumTestEnlistmentFile().FullName));
        Assert.IsTrue(loaded.Wait(TimeSpan.FromSeconds(15)), "Files not loaded");

        // Cancel strictly mid-batch when the first file piece is visited inside SearchCodeWorker.
        using (var cts = new CancellationTokenSource()) {
          onFileVisited = () => cts.Cancel();
          Assert.Throws<OperationCanceledException>(() =>
            searchEngine.SearchCode(new EngineSearchParams {
              SearchString = "directory",
              MaxResults = 2000,
            }, cts.Token));
        }

        // Subsequent search without cancellation returns full results.
        onFileVisited = null;
        var ok = searchEngine.SearchCode(new EngineSearchParams {
          SearchString = "Test directory looking like",
          MaxResults = 2000,
          MatchCase = true,
        });
        Assert.AreEqual(1, ok.HitCount);
      }
    }

    [TestMethod]
    public void JsonRpcCancelRequestAbortsInFlightCodeSearchWithRequestCancelledError() {
      using (var container = CreateServerServices()) {
        var inSearch = new ManualResetEventSlim(false);
        var allowProceed = new ManualResetEventSlim(false);
        var blockNextSearch = false;

        var interceptingFactory = new InterceptingSearchDataFactory(
          container.CompiledTextSearchDataFactory,
          () => {
            if (Volatile.Read(ref blockNextSearch)) {
              inSearch.Set();
              allowProceed.Wait(TimeSpan.FromSeconds(10));
            }
          });

        var searchEngine = new SearchEngine(
          container.FileSystemSnapshotManager,
          container.FileSystemNameFactory,
          container.LongRunningFileSystemTaskQueue,
          container.FileDatabaseSnapshotFactory,
          container.ProjectDiscovery,
          interceptingFactory,
          container.OperationProcessor,
          container.IndexingWorkerPool);

        var loaded = new ManualResetEventSlim(false);
        searchEngine.FilesLoaded += (_, _) => loaded.Set();
        container.FileRegistrationTracker.RegisterFileAsync(new FullPath(Utils.GetChromiumTestEnlistmentFile().FullName));
        Assert.IsTrue(loaded.Wait(TimeSpan.FromSeconds(15)), "Files not loaded");

        var handler = new ProtocolRequestHandler(
          container.FileSystemSnapshotManager,
          searchEngine,
          container.FileRegistrationTracker,
          container.IndexingServer,
          container.ArenaAllocators);
        var output = new JsonRpcOutput();
        var connection = new JsonRpcConnection(handler, output);

        using (var clientToServer = new PipeStream())
        using (var serverToClient = new PipeStream()) {
          output.Connect(serverToClient);
          var serverTask = Task.Run(() => connection.Run(clientToServer));
          var writer = new JsonRpcMessageWriter(clientToServer);
          var reader = new JsonRpcMessageReader(serverToClient);
          var bufferedResponses = new Dictionary<int, JsonDocument>();

          void SendJson(string json) {
            writer.WriteMessage(Encoding.UTF8.GetBytes(json));
          }

          JsonDocument ReadResponseForId(int expectedId) {
            if (bufferedResponses.Remove(expectedId, out var buffered)) {
              return buffered;
            }
            while (true) {
              var bytes = reader.ReadMessage();
              Assert.IsNotNull(bytes);
              var doc = JsonDocument.Parse(bytes);
              if (doc.RootElement.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.Number) {
                var id = idProp.GetInt32();
                if (id == expectedId) {
                  return doc;
                }
                bufferedResponses[id] = doc;
              } else {
                doc.Dispose();
              }
            }
          }

          Volatile.Write(ref blockNextSearch, true);
          SendJson("{\"jsonrpc\":\"2.0\",\"id\":42,\"method\":\"search/code\",\"params\":{\"query\":{\"text\":\"directory\"},\"maxResults\":100}}");
          Assert.IsTrue(inSearch.Wait(TimeSpan.FromSeconds(10)), "Search did not start");

          // Send $/cancelRequest while search/code is mid-execution, followed by a round-trip probe
          // so the connection reader thread is guaranteed to have processed $/cancelRequest before
          // releasing the blocked search worker.
          SendJson("{\"jsonrpc\":\"2.0\",\"method\":\"$/cancelRequest\",\"params\":{\"id\":42}}");
          SendJson("{\"jsonrpc\":\"2.0\",\"id\":99,\"method\":\"tree/get\",\"params\":{}}");
          using (var probe = ReadResponseForId(99)) {
            Assert.IsTrue(probe.RootElement.TryGetProperty("result", out _));
          }
          Volatile.Write(ref blockNextSearch, false);
          allowProceed.Set();

          using (var response = ReadResponseForId(42)) {
            Assert.IsTrue(response.RootElement.TryGetProperty("error", out var error));
            Assert.AreEqual(JsonRpcErrorCodes.RequestCancelled, error.GetProperty("code").GetInt32());
          }

          // Subsequent search succeeds over the same JSON-RPC connection.
          SendJson("{\"jsonrpc\":\"2.0\",\"id\":43,\"method\":\"search/code\",\"params\":{\"query\":{\"text\":\"Test directory looking like\",\"matchCase\":true},\"maxResults\":100}}");
          using (var response = ReadResponseForId(43)) {
            Assert.AreEqual(1, response.RootElement.GetProperty("result").GetProperty("hitCount").GetInt32());
          }

          clientToServer.CompleteWriting();
          Assert.IsTrue(serverTask.Wait(TimeSpan.FromSeconds(10)));
        }
      }
    }

    [TestMethod]
    public void TaskCancellationScopeAndCancelAllAreThreadSafe() {
      var tc = new TaskCancellation();
      Parallel.For(0, 500, i => {
        using var cts = new CancellationTokenSource();
        using var scope = tc.CreateScope(cts.Token);
        if (i % 7 == 0) {
          tc.CancelAll();
        }
        if (i % 11 == 0) {
          cts.Cancel();
          Assert.IsTrue(scope.Token.IsCancellationRequested);
        }
      });
    }

    private sealed class InterceptingSearchDataFactory : ICompiledTextSearchDataFactory {
      private readonly ICompiledTextSearchDataFactory _inner;
      private readonly Action _onFileVisited;

      public InterceptingSearchDataFactory(ICompiledTextSearchDataFactory inner, Action onFileVisited) {
        _inner = inner;
        _onFileVisited = onFileVisited;
      }

      public CompiledTextSearchData Create(EngineSearchParams searchParams, Func<FileName, bool> fileNamePathMatcher) {
        return _inner.Create(searchParams, fileName => {
          _onFileVisited();
          return fileNamePathMatcher(fileName);
        });
      }
    }
  }
}
