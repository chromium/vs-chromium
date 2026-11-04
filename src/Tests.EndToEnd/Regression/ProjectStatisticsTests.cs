// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Per project statistics and actions ("project/details"): the file contents of each project are
/// allocated in its own arenas (memory split exactly, arenas released when the project is removed).
/// </summary>
[TestClass]
public class ProjectStatisticsTests {
  public TestContext TestContext { get; set; } = null!;

  private const string ProjectFileName = "vs-chromium-project.txt";

  /// <summary>A generated project (deleted when disposed).</summary>
  private sealed class TestProject : IDisposable {
    public TestProject(string name, int fileCount, int linesPerFile) {
      Root = Path.Combine(Path.GetTempPath(), $"VsChromiumProjectStatisticsTest-{name}-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(Path.Combine(Root, "src"));
      File.WriteAllText(ProjectFile, "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
      for (var i = 0; i < fileCount; i++) {
        var sb = new StringBuilder();
        sb.Append("// File ").Append(i).Append(" of ").Append(name).Append(" PROJECT_MARKER_").Append(name).Append('\n');
        for (var line = 0; line < linesPerFile; line++) {
          sb.Append("int function_").Append(i).Append('_').Append(line).Append("() { return ").Append(line * i).Append("; }\n");
        }
        File.WriteAllText(Path.Combine(Root, "src", $"file{i:D4}.cc"), sb.ToString());
        TextBytes += sb.Length;
      }
      TextBytes += new FileInfo(ProjectFile).Length;
    }

    public string Root { get; }
    public string ProjectFile => Path.Combine(Root, ProjectFileName);
    /// <summary>The bytes of the text of the files of the project.</summary>
    public long TextBytes { get; }

    public void Dispose() {
      try {
        Directory.Delete(Root, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        // Best effort
      }
    }
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task EachProjectHasItsOwnArenas() {
    using var a = new TestProject("A", fileCount: 600, linesPerFile: 60);
    using var b = new TestProject("B", fileCount: 200, linesPerFile: 30);
    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    await server.RequestAsync(ServerProtocol.RegisterFile(a.ProjectFile));
    await server.RequestAsync(ServerProtocol.RegisterFile(b.ProjectFile));
    await WaitForAsync(server, async () => {
      var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
      return status["projectCount"]!.GetValue<int>() == 2 && status["status"]!.GetValue<string>() == "idle" &&
             status["fileCount"]!.GetValue<int>() == 600 + 200 + 2;
    }, "both projects loaded");

    var memoryA = (await DetailsAsync(server, a.Root))["memory"]!;
    var memoryB = (await DetailsAsync(server, b.Root))["memory"]!;
    var arenas = (await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["fileContentsArenas"]!;
    TestContext.WriteLine($"A: {memoryA.ToJsonString()}\nB: {memoryB.ToJsonString()}\nall: {arenas.ToJsonString()}");

    // The text of each project is in its arenas (slots: the text and 2 trailing bytes, aligned).
    foreach (var (project, memory, fileCount) in new[] { (a, memoryA, 601), (b, memoryB, 201) }) {
      var live = memory["liveBytes"]!.GetValue<long>();
      Assert.IsTrue(live >= project.TextBytes && live <= project.TextBytes + fileCount * 10, $"{live} for {project.TextBytes} bytes of text");
      Assert.AreEqual(0, memory["deadBytes"]!.GetValue<long>());
      Assert.AreEqual(1, memory["arenaCount"]!.GetValue<int>());
      Assert.AreEqual(16L * 1024 * 1024, memory["arenaBytes"]!.GetValue<long>());
      Assert.AreEqual(memory["arenaBytes"]!.GetValue<long>() - live, memory["freeBytes"]!.GetValue<long>());
      Assert.AreEqual(memory["arenaBytes"]!.GetValue<long>(), memory["nativeBytes"]!.GetValue<long>());
      Assert.AreEqual(0, memory["largeFileBytes"]!.GetValue<long>());
      Assert.AreEqual(0, memory["mappedCacheBytes"]!.GetValue<long>());
    }
    // The process-wide statistics are the sums of the projects.
    Assert.AreEqual(2, arenas["arenaCount"]!.GetValue<int>());
    Assert.AreEqual(memoryA["liveBytes"]!.GetValue<long>() + memoryB["liveBytes"]!.GetValue<long>(), arenas["liveBytes"]!.GetValue<long>());
    Assert.AreEqual(memoryA["arenaBytes"]!.GetValue<long>() + memoryB["arenaBytes"]!.GetValue<long>(), arenas["arenaBytes"]!.GetValue<long>());

    // Edits are dead bytes of the arenas of their project only.
    var edited = Path.Combine(b.Root, "src", "file0000.cc");
    File.AppendAllText(edited, "// EDITED\n");
    await WaitForAsync(server, async () =>
      (await DetailsAsync(server, b.Root))["memory"]!["deadBytes"]!.GetValue<long>() > 0, "edit indexed");
    Assert.AreEqual(0, (await DetailsAsync(server, a.Root))["memory"]!["deadBytes"]!.GetValue<long>());

    // Removing a project releases its arenas (once its contents are not used anymore).
    var nativeBefore = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["nativeAllocatedBytes"]!.GetValue<long>();
    await server.RequestAsync(ServerProtocol.UnregisterFile(b.ProjectFile));
    await WaitForAsync(server, async () => {
      var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
      return status["projectCount"]!.GetValue<int>() == 1 && status["status"]!.GetValue<string>() == "idle";
    }, "project B removed");
    await WaitForAsync(server, async () => {
      var statistics = (await server.RequestAsync(ServerProtocol.GetServerStatistics(forceGc: true))).Data;
      return statistics["fileContentsArenas"]!["arenaCount"]!.GetValue<int>() == 1 &&
             statistics["nativeAllocatedBytes"]!.GetValue<long>() <= nativeBefore - 16L * 1024 * 1024;
    }, "arenas of project B released");
    var remaining = (await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["fileContentsArenas"]!;
    Assert.AreEqual(memoryA["liveBytes"]!.GetValue<long>(), remaining["liveBytes"]!.GetValue<long>());
  }

  /// <summary>
  /// The cache of each project: its directory, size and save state, and the contents mapped from it
  /// after a restart.
  /// </summary>
  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task EachProjectHasItsOwnCache() {
    using var a = new TestProject("A", fileCount: 300, linesPerFile: 40);
    using var b = new TestProject("B", fileCount: 100, linesPerFile: 40);
    var cacheRoot = Path.Combine(Path.GetTempPath(), "VsChromiumProjectStatisticsTestCache-" + Guid.NewGuid().ToString("N"));
    try {
      // Files older than the "racy" interval of the cache.
      foreach (var file in Directory.GetFiles(a.Root, "*", SearchOption.AllDirectories).Concat(Directory.GetFiles(b.Root, "*", SearchOption.AllDirectories))) {
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-1));
      }
      await using (var server = await StartAsync(cacheRoot, a, b)) {
        await WaitForAsync(server, async () =>
          (await DetailsAsync(server, a.Root))["cache"]!["lastSavedUtc"] != null &&
          (await DetailsAsync(server, b.Root))["cache"]!["lastSavedUtc"] != null, "caches saved");
        var cacheA = (await DetailsAsync(server, a.Root))["cache"]!;
        var cacheB = (await DetailsAsync(server, b.Root))["cache"]!;
        TestContext.WriteLine($"A: {cacheA.ToJsonString()}\nB: {cacheB.ToJsonString()}");
        foreach (var cache in new[] { cacheA, cacheB }) {
          Assert.IsTrue(cache["enabled"]!.GetValue<bool>());
          Assert.IsTrue(cache["writable"]!.GetValue<bool>());
          var directory = cache["directory"]!.GetValue<string>();
          Assert.AreEqual(cacheRoot, Path.GetDirectoryName(directory));
          Assert.IsTrue(File.Exists(Path.Combine(directory, "index.bin")));
          Assert.IsTrue(cache["sizeOnDiskBytes"]!.GetValue<long>() > 0);
          Assert.AreEqual(0, cache["deadBytes"]!.GetValue<long>());
          Assert.IsFalse(cache["saving"]!.GetValue<bool>());
          Assert.IsFalse(cache["dirty"]!.GetValue<bool>());
        }
        Assert.AreNotEqual(cacheA["directory"]!.GetValue<string>(), cacheB["directory"]!.GetValue<string>());
        Assert.IsTrue(cacheA["sizeOnDiskBytes"]!.GetValue<long>() > cacheB["sizeOnDiskBytes"]!.GetValue<long>());

        // An edit makes its project dirty only.
        File.AppendAllText(Path.Combine(a.Root, "src", "file0001.cc"), "// EDITED\n");
        await WaitForAsync(server, async () => (await DetailsAsync(server, a.Root))["cache"]!["dirty"]!.GetValue<bool>(), "A dirty");
        Assert.IsFalse((await DetailsAsync(server, b.Root))["cache"]!["dirty"]!.GetValue<bool>(), "B unchanged");
      }

      // After a restart, the contents come from the caches (mapped), each project its own.
      await using (var server = await StartAsync(cacheRoot, a, b)) {
        var memoryA = (await DetailsAsync(server, a.Root))["memory"]!;
        var memoryB = (await DetailsAsync(server, b.Root))["memory"]!;
        TestContext.WriteLine($"A: {memoryA.ToJsonString()}\nB: {memoryB.ToJsonString()}");
        Assert.IsTrue(memoryA["mappedCacheBytes"]!.GetValue<long>() >= a.TextBytes - 1000, memoryA.ToJsonString());
        Assert.IsTrue(memoryB["mappedCacheBytes"]!.GetValue<long>() >= b.TextBytes, memoryB.ToJsonString());
        Assert.IsTrue(memoryA["mappedCacheBytes"]!.GetValue<long>() <= a.TextBytes + 2000, memoryA.ToJsonString());
        var arenas = (await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["fileContentsArenas"]!;
        Assert.AreEqual(memoryA["mappedCacheBytes"]!.GetValue<long>() + memoryB["mappedCacheBytes"]!.GetValue<long>(),
          arenas["cachedFileBytes"]!.GetValue<long>());
      }
    }
    finally {
      try {
        Directory.Delete(cacheRoot, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      }
    }
  }

  /// <summary>
  /// The initial loads and incremental updates of each project: scan and load times, loads from the
  /// cache (time to the published index of the cache, then to the verified index).
  /// </summary>
  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task EachProjectHasItsOwnOperationStatistics() {
    using var a = new TestProject("A", fileCount: 600, linesPerFile: 40);
    using var b = new TestProject("B", fileCount: 200, linesPerFile: 40);
    var cacheRoot = Path.Combine(Path.GetTempPath(), "VsChromiumProjectStatisticsTestCache-" + Guid.NewGuid().ToString("N"));
    try {
      foreach (var file in Directory.GetFiles(a.Root, "*", SearchOption.AllDirectories).Concat(Directory.GetFiles(b.Root, "*", SearchOption.AllDirectories))) {
        File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddHours(-1));
      }
      await using (var server = await StartAsync(cacheRoot, a, b)) {
        var operationsA = (await DetailsAsync(server, a.Root))["operations"]!;
        var operationsB = (await DetailsAsync(server, b.Root))["operations"]!;
        TestContext.WriteLine($"A: {operationsA.ToJsonString()}\nB: {operationsB.ToJsonString()}");
        foreach (var operations in new[] { operationsA, operationsB }) {
          var load = operations["initialLoad"]!;
          Assert.AreEqual(1, load["count"]!.GetValue<long>());
          Assert.IsFalse(load["fromCache"]!.GetValue<bool>());
          Assert.IsNull(load["cachedIndexMs"]);
          var lastMs = load["lastMs"]!.GetValue<double>();
          Assert.IsTrue(lastMs > 0);
          Assert.AreEqual(lastMs, load["minMs"]!.GetValue<double>());
          Assert.AreEqual(lastMs, load["maxMs"]!.GetValue<double>());
          Assert.AreEqual(lastMs, load["avgMs"]!.GetValue<double>());
          var scanMs = load["scanMs"]!.GetValue<double>();
          Assert.IsTrue(scanMs > 0 && scanMs <= lastMs, $"scan {scanMs} ms, load {lastMs} ms");
          Assert.AreEqual(0, operations["incrementalUpdates"]!["count"]!.GetValue<long>());
        }

        // An edit is an incremental update of its project only.
        File.AppendAllText(Path.Combine(a.Root, "src", "file0001.cc"), "// EDITED\n");
        await WaitForAsync(server, async () =>
          (await DetailsAsync(server, a.Root))["operations"]!["incrementalUpdates"]!["count"]!.GetValue<long>() >= 1, "update of A");
        var updates = (await DetailsAsync(server, a.Root))["operations"]!["incrementalUpdates"]!;
        Assert.IsTrue(updates["lastMs"]!.GetValue<double>() > 0);
        Assert.IsTrue(updates["minMs"]!.GetValue<double>() <= updates["maxMs"]!.GetValue<double>());
        Assert.AreEqual(0, (await DetailsAsync(server, b.Root))["operations"]!["incrementalUpdates"]!["count"]!.GetValue<long>());
        Assert.AreEqual(1, (await DetailsAsync(server, a.Root))["operations"]!["initialLoad"]!["count"]!.GetValue<long>());
        await WaitForAsync(server, async () =>
          !(await DetailsAsync(server, a.Root))["cache"]!["dirty"]!.GetValue<bool>(), "A saved");
      }

      // A restart: loads from the cache, then verified.
      await using (var server = await StartAsync(cacheRoot, a, b)) {
        foreach (var project in new[] { a, b }) {
          var load = (await DetailsAsync(server, project.Root))["operations"]!["initialLoad"]!;
          TestContext.WriteLine($"{project.Root}: {load.ToJsonString()}");
          Assert.AreEqual(1, load["count"]!.GetValue<long>());
          Assert.IsTrue(load["fromCache"]!.GetValue<bool>(), load.ToJsonString());
          var cachedIndexMs = load["cachedIndexMs"]!.GetValue<double>();
          Assert.IsTrue(cachedIndexMs > 0 && cachedIndexMs <= load["lastMs"]!.GetValue<double>(), load.ToJsonString());
          Assert.IsTrue(load["scanMs"]!.GetValue<double>() > 0, "Verification scan");
        }
      }
    }
    finally {
      try {
        Directory.Delete(cacheRoot, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      }
    }
  }

  /// <summary>Starts a server with the cache <paramref name="cacheRoot"/> and waits for the projects to be loaded.</summary>
  private static async Task<JsonRpcServerClient> StartAsync(string cacheRoot, params TestProject[] projects) {
    var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath, cacheRoot,
      new Dictionary<string, string?> { ["VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS"] = "500,2000" });
    foreach (var project in projects) {
      await server.RequestAsync(ServerProtocol.RegisterFile(project.ProjectFile));
    }
    await WaitForAsync(server, async () => {
      var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
      return status["projectCount"]!.GetValue<int>() == projects.Length && status["status"]!.GetValue<string>() == "idle";
    }, "projects loaded");
    return server;
  }

  private static async Task<JsonObject> DetailsAsync(JsonRpcServerClient server, string root) {
    return (await server.RequestAsync(ServerProtocol.GetProjectDetails(root))).Data;
  }

  private static async Task WaitForAsync(JsonRpcServerClient server, Func<Task<bool>> condition, string description) {
    var stopwatch = Stopwatch.StartNew();
    while (!await condition()) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Timeout waiting for: {description}");
      await Task.Delay(200);
    }
  }
}
