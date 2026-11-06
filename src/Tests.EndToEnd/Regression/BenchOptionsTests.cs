// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// The benchmark options of the server (used by vschromium-bench, src/Tools/Bench): "--bench" and the
/// "bench/*" requests, "--threads", "--watcher-delay-ms", "--no-journal".
/// </summary>
[TestClass]
public class BenchOptionsTests {
  private const int FileCount = 200;
  private const string Marker = "BENCH_OPTIONS_MARKER";

  private static string CreateTree() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumBenchOptionsTest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "src"));
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    for (var i = 0; i < FileCount; i++) {
      var sb = new StringBuilder();
      sb.Append("// File ").Append(i).Append(" with ").Append(Marker).Append('\n');
      for (var line = 0; line < 20; line++) {
        sb.Append("int function_").Append(i).Append('_').Append(line).Append("() { return ").Append(line).Append("; }\n");
      }
      File.WriteAllText(Path.Combine(root, "src", $"file{i:D3}.cc"), sb.ToString());
    }
    return root;
  }

  private static void DeleteDirectory(string path) {
    try {
      Directory.Delete(path, recursive: true);
    }
    catch (IOException) {
      // Best effort
    }
  }

  private static Task<JsonRpcServerClient> StartAsync(string cacheDirectory, params string[] arguments) =>
    JsonRpcServerClient.StartAsync(TestSettings.ServerPath, new ServerStartOptions { CacheDirectory = cacheDirectory, ExtraArguments = arguments });

  private static Task RegisterAndWaitAsync(JsonRpcServerClient server, string root, Func<JsonObject, bool>? predicate = null) =>
    server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task BenchRequests_NeedTheBenchOption() {
    var root = CreateTree();
    var cache = Path.Combine(Path.GetTempPath(), "VsChromiumBenchOptionsCache-" + Guid.NewGuid().ToString("N"));
    try {
      await using (var server = await StartAsync(cache)) {
        Assert.IsNull(server.InitializeResult["bench"]);
        var error = await Assert.ThrowsExactlyAsync<ServerErrorException>(() =>
          server.RequestAsync(new ServerRequest("bench/saveCache", new JsonObject())));
        Assert.AreEqual(-32601, error.Code);
      }

      await using (var server = await StartAsync(cache, "--bench", "--threads", "2", "--watcher-delay-ms", "0")) {
        var requests = server.InitializeResult["bench"]!["requests"]!.AsArray().Select(n => (string?)n).ToList();
        CollectionAssert.AreEquivalent(new[] { "bench/saveCache", "bench/phases" }, requests);
        await RegisterAndWaitAsync(server, root);

        // Saved now (the first load of the project), then nothing to save.
        var saved = (await server.RequestAsync(new ServerRequest("bench/saveCache", new JsonObject()))).Data;
        Assert.IsTrue(saved["bytes"]!.GetValue<long>() > 0);
        // Note: The files were just written: their contents are not in the cache yet (read from disk by the next load).
        Assert.IsTrue(saved["entryCount"]!.GetValue<int>() >= 0);
        var again = (await server.RequestAsync(new ServerRequest("bench/saveCache", new JsonObject()))).Data;
        Assert.IsFalse(again["saved"]!.GetValue<bool>());

        var phases = (await server.RequestAsync(new ServerRequest("bench/phases", new JsonObject { ["projectPath"] = root }))).Data;
        var names = phases["phases"]!.AsArray().Select(p => (string?)p!["name"]).ToList();
        CollectionAssert.Contains(names, "FileSystemScan");
        Assert.IsTrue(phases["initialLoadMs"]!.GetValue<double>() > 0);
        Assert.IsTrue(phases["cpuUserMs"]!.GetValue<double>() > 0);

        // Changes are processed without the debouncing delay (--watcher-delay-ms 0).
        var file = Path.Combine(root, "src", "file000.cc");
        await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromSeconds(30),
          () => { File.AppendAllText(file, "// BENCH_OPTIONS_CHANGE\n"); return Task.CompletedTask; });
        var hits = (await server.RequestAsync(ServerProtocol.SearchCode("BENCH_OPTIONS_CHANGE", matchCase: true))).Data["hitCount"]!.GetValue<long>();
        Assert.AreEqual(1, hits);
      }
    } finally {
      DeleteDirectory(root);
      DeleteDirectory(cache);
    }
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task NoJournal_VerifiesTheCacheByAFullScan() {
    var root = CreateTree();
    var cache = Path.Combine(Path.GetTempPath(), "VsChromiumBenchOptionsCache-" + Guid.NewGuid().ToString("N"));
    try {
      await using (var server = await StartAsync(cache, "--bench")) {
        await RegisterAndWaitAsync(server, root);
        await server.RequestAsync(new ServerRequest("bench/saveCache", new JsonObject()));
      }
      await using (var server = await StartAsync(cache, "--bench", "--no-journal")) {
        var fromCache = await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
          () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));
        Assert.IsTrue((bool?)fromCache["fromCache"] == true, "loaded from the cache");
        await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2));
        var details = (await server.RequestAsync(ServerProtocol.GetProjectDetails(root))).Data;
        var verification = details["operations"]!["lastVerification"]!;
        Assert.AreEqual("fullScan", (string?)verification["method"]);
        if (OperatingSystem.IsWindows()) {
          StringAssert.Contains((string?)verification["fallbackReason"], "--no-journal");
        }
      }
    } finally {
      DeleteDirectory(root);
      DeleteDirectory(cache);
    }
  }

  [TestMethod]
  [Timeout(60 * 1000, CooperativeCancellation = true)]
  public async Task InvalidBenchOption_ExitsWithAnError() {
    // The server exits at startup (the error is on its standard error).
    await Assert.ThrowsAsync<Exception>(() => StartAsync(null!, "--threads", "many"));
  }
}
