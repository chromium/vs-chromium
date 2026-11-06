// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;
using VsChromium.Bench.Shared.Corpus;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Tests.EndToEnd.Protocol;

/// <summary>
/// The JSON-RPC protocol of the server ("--jsonrpc", see protocol/README.md), over the standard
/// input/output of the server executable: initialization, notifications, the tree requests, error
/// codes, cancellation.
/// </summary>
[TestClass]
public class JsonRpcProtocolTests {
  private const string Marker = "JSONRPC_PROTOCOL_MARKER";

  private static void GenerateFiles(string root) {
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    Directory.CreateDirectory(Path.Combine(root, "src", "sub"));
    File.WriteAllText(Path.Combine(root, "src", "a.cc"), "// " + Marker + "\n");
    File.WriteAllText(Path.Combine(root, "src", "sub", "b.h"), "// " + Marker + " " + Marker + "\n");
    File.WriteAllText(Path.Combine(root, "readme.txt"), "no marker here\n");
  }

  private static string Root => CorpusCache.GetOrCreateCustom(TestSettings.CorporaDirectory, "jsonrpc-protocol", version: 1, GenerateFiles);

  private static async Task<JsonRpcServerClient> StartIndexedServerAsync(List<string>? notifications = null) {
    var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    using (server.OnNotification((method, _) => {
             if (notifications != null) {
               lock (notifications) { notifications.Add(method); }
             }
           })) {
      await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(1),
        () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(Root, "vs-chromium-project.txt"))));
    }
    return server;
  }

  [TestMethod]
  public async Task InitializeReturnsTheServerVersions() {
    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    Assert.AreEqual(1, server.InitializeResult["protocolVersion"]!.GetValue<int>());
    Assert.AreEqual(server.Process.Id, server.InitializeResult["serverProcessId"]!.GetValue<int>());
    Assert.IsFalse(string.IsNullOrEmpty((string?)server.InitializeResult["serverVersion"]));
  }

  [TestMethod]
  public async Task IndexingSendsNotifications() {
    var notifications = new List<string>();
    await using var server = await StartIndexedServerAsync(notifications);
    List<string> received;
    lock (notifications) { received = notifications.ToList(); }
    var scanStarted = received.IndexOf("index/scanStarted");
    var scanFinished = received.IndexOf("index/scanFinished");
    var loading = received.IndexOf("index/loading");
    var loaded = received.IndexOf("index/loaded");
    // (Events of different services are raised on different threads: only the order of the events
    // of the same operation is guaranteed.)
    Assert.IsTrue(scanStarted >= 0 && scanStarted < scanFinished && loading >= 0 && loading < loaded,
      string.Join(", ", received));
  }

  [TestMethod]
  public async Task SearchResultsAreFlatListsOfFilesPerProject() {
    await using var server = await StartIndexedServerAsync();
    var response = await server.RequestAsync(ServerProtocol.SearchCode(Marker, matchCase: true));
    Assert.AreEqual(3, response.Data["hitCount"]!.GetValue<int>());
    var project = response.Data["projects"]!.AsArray().Single()!;
    Assert.AreEqual(Root, (string?)project["rootPath"]);
    var files = project["files"]!.AsArray().Select(f => ((string)f!["relativePath"]!, f["matches"]!.AsArray().Count)).ToList();
    CollectionAssert.AreEqual(new[] { (Path.Combine("src", "a.cc"), 1), (Path.Combine("src", "sub", "b.h"), 2) }, files);

    var paths = await server.RequestAsync(ServerProtocol.SearchFilePaths("*.cc"));
    Assert.AreEqual(1, paths.Data["hitCount"]!.GetValue<int>());
    CollectionAssert.AreEqual(new[] { Path.Combine(Root, "src", "a.cc") },
      ServerProtocol.FlattenPathResults(paths.Data).Select(x => x.FullPath).ToList());
  }

  [TestMethod]
  public async Task TreeRequestsWork() {
    await using var server = await StartIndexedServerAsync();
    var tree = (await server.RequestAsync(new ServerRequest("tree/get", new JsonObject()))).Data;
    Assert.AreEqual(Root, (string?)tree["projects"]!.AsArray().Single()!["rootPath"]);
    var version = tree["version"]!.GetValue<int>();
    var unchanged = (await server.RequestAsync(new ServerRequest("tree/get", new JsonObject { ["knownVersion"] = version }))).Data;
    Assert.AreEqual(version, unchanged["version"]!.GetValue<int>());
    Assert.AreEqual(0, unchanged["projects"]!.AsArray().Count);

    var entries = (await server.RequestAsync(new ServerRequest("tree/entries",
      new JsonObject { ["projectPath"] = Root, ["relativePath"] = "src" }))).Data;
    Assert.IsTrue(entries["exists"]!.GetValue<bool>());
    CollectionAssert.AreEqual(new[] { "sub" }, entries["directoryNames"]!.AsArray().Select(x => (string?)x).ToArray());
    CollectionAssert.AreEqual(new[] { "a.cc" }, entries["fileNames"]!.AsArray().Select(x => (string?)x).ToArray());

    var multiple = (await server.RequestAsync(new ServerRequest("tree/entriesMultiple",
      new JsonObject { ["projectPath"] = Root, ["relativePaths"] = new JsonArray("", Path.Combine("src", "sub"), "missing") }))).Data;
    var directories = multiple["directories"]!.AsArray();
    Assert.AreEqual(3, directories.Count);
    CollectionAssert.AreEqual(new[] { "src" }, directories[0]!["directoryNames"]!.AsArray().Select(x => (string?)x).ToArray());
    CollectionAssert.AreEqual(new[] { "b.h" }, directories[1]!["fileNames"]!.AsArray().Select(x => (string?)x).ToArray());
    Assert.IsFalse(directories[2]!["exists"]!.GetValue<bool>());
    Assert.AreEqual("missing", (string?)directories[2]!["relativePath"]);

    var details = (await server.RequestAsync(new ServerRequest("tree/directoryDetails",
      new JsonObject { ["path"] = Path.Combine(Root, "src"), ["maxFilesByExtensionDetailsCount"] = 10, ["maxLargeFilesDetailsCount"] = 10 }))).Data;
    Assert.AreEqual(2, details["fileCount"]!.GetValue<int>());
    Assert.AreEqual(2, details["searchableFilesCount"]!.GetValue<int>());
  }

  [TestMethod]
  public async Task ErrorsHaveJsonRpcErrorCodes() {
    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    async Task<int> ErrorCode(ServerRequest request) =>
      (await Assert.ThrowsExactlyAsync<ServerErrorException>(() => server.RequestAsync(request))).Code;

    Assert.AreEqual(-32601, await ErrorCode(new ServerRequest("foo/bar", new JsonObject())), "Unknown method");
    Assert.AreEqual(-32602, await ErrorCode(new ServerRequest("files/register", new JsonObject { ["path"] = 12 })), "Invalid params");
    Assert.AreEqual(-32001, await ErrorCode(ServerProtocol.GetProjectDetails(Path.Combine(Root, "unknown"))), "Recoverable error");
    Assert.AreEqual(-32001, await ErrorCode(ServerProtocol.SearchCode("(", regex: true)), "Invalid regex");
  }

  [TestMethod]
  public async Task CancelledRequestsFailOrComplete() {
    await using var server = await StartIndexedServerAsync();
    // Searches of the small test tree are too fast to be cancelled reliably: the request either
    // completes or fails with "RequestCancelled", and the server keeps working.
    var search = server.RequestAsync(ServerProtocol.SearchCode(Marker, matchCase: true), out var requestId);
    server.CancelRequest(requestId);
    server.CancelRequest(123456); // Unknown ids are ignored
    try {
      var response = await search;
      Assert.AreEqual(3, response.Data["hitCount"]!.GetValue<int>());
    } catch (ServerErrorException e) {
      Assert.AreEqual(-32800, e.Code);
    }
    var again = await server.RequestAsync(ServerProtocol.SearchCode(Marker, matchCase: true));
    Assert.AreEqual(3, again.Data["hitCount"]!.GetValue<int>());
  }

  [TestMethod]
  public async Task PauseAndResumeChangeTheIndexingState() {
    await using var server = await StartIndexedServerAsync();
    // The state changes asynchronously, after the response ("index/stateChanged" notification).
    await server.WaitForNotificationAsync("index/stateChanged", TimeSpan.FromSeconds(30),
      () => server.RequestAsync(new ServerRequest("index/pause", new JsonObject())));
    await WaitForStatusAsync(server, status => status == "paused");
    await server.WaitForNotificationAsync("index/stateChanged", TimeSpan.FromSeconds(30),
      () => server.RequestAsync(new ServerRequest("index/resume", new JsonObject())));
    await WaitForStatusAsync(server, status => status != "paused");
  }

  private static async Task WaitForStatusAsync(JsonRpcServerClient server, Func<string?, bool> predicate) {
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    while (true) {
      var statistics = await server.RequestAsync(ServerProtocol.GetIndexStatistics());
      var status = (string?)statistics.Data["status"];
      if (predicate(status)) {
        return;
      }
      if (stopwatch.Elapsed > TimeSpan.FromSeconds(30)) {
        Assert.Fail($"Unexpected indexing status \"{status}\"");
      }
      await Task.Delay(50);
    }
  }
}
