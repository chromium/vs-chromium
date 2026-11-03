// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Per project options ("project/setOptions"): trigram filters (disabled by default) can be enabled
/// and disabled at any time; search results never change, only the memory used by the filters.
/// </summary>
[TestClass]
public class ProjectOptionsTests {
  private const int FileCount = 300;
  private const string Marker = "PROJECT_OPTIONS_MARKER";

  private static string CreateTree() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumProjectOptionsTest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "src"));
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    for (var i = 0; i < FileCount; i++) {
      var sb = new StringBuilder();
      sb.Append("// File ").Append(i).Append(" with ").Append(Marker).Append('\n');
      for (var line = 0; line < 50; line++) {
        sb.Append("int function_").Append(i).Append('_').Append(line).Append("() { return ").Append(line * i).Append("; }\n");
      }
      File.WriteAllText(Path.Combine(root, "src", $"file{i:D3}.cc"), sb.ToString());
    }
    return root;
  }

  private static void DeleteTree(string root) {
    try {
      Directory.Delete(root, recursive: true);
    }
    catch (IOException) {
      // Best effort
    }
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task TrigramFilters_CanBeDisabledAndEnabledAtAnyTime() {
    var root = CreateTree();
    try {
      await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
      await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
        () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));

      // Disabled by default: no filters are built after the load.
      await Task.Delay(TimeSpan.FromSeconds(1));
      Assert.AreEqual(0, await TrigramFiltersBytesAsync(server));
      Assert.IsFalse((await ProjectDetailsAsync(server, root))["trigramFiltersEnabled"]!.GetValue<bool>());

      // Enable (any path inside the project works): built in the background.
      await server.RequestAsync(ServerProtocol.SetProjectOptions(Path.Combine(root, "src"), true));
      await WaitForAsync(async () => await TrigramFiltersBytesAsync(server) > 0, "filters built");
      Assert.AreEqual(FileCount, await HitCountAsync(server));
      var details = await ProjectDetailsAsync(server, root);
      Assert.IsTrue(details["trigramFiltersEnabled"]!.GetValue<bool>());
      Assert.IsTrue(details["trigramFiltersBytes"]!.GetValue<long>() > 0);

      // Disable: the filters are released.
      await server.RequestAsync(ServerProtocol.SetProjectOptions(Path.Combine(root, "src"), false));
      await WaitForAsync(async () => await TrigramFiltersBytesAsync(server) == 0, "filters released");
      Assert.AreEqual(FileCount, await HitCountAsync(server));
      details = await ProjectDetailsAsync(server, root);
      Assert.IsFalse(details["trigramFiltersEnabled"]!.GetValue<bool>());
      Assert.AreEqual(0, details["trigramFiltersBytes"]!.GetValue<long>());

      // Incremental updates don't build filters for the project.
      File.AppendAllText(Path.Combine(root, "src", "file000.cc"), "// " + Marker + " again\n");
      await WaitForAsync(async () => await HitCountAsync(server) == FileCount + 1, "change indexed");
      await Task.Delay(TimeSpan.FromSeconds(1));
      Assert.AreEqual(0, await TrigramFiltersBytesAsync(server));

      // A null option leaves the setting unchanged.
      await server.RequestAsync(ServerProtocol.SetProjectOptions(root, null));
      details = await ProjectDetailsAsync(server, root);
      Assert.IsFalse(details["trigramFiltersEnabled"]!.GetValue<bool>());

      // Enable again: the filters are built again, results are the same.
      await server.RequestAsync(ServerProtocol.SetProjectOptions(root, true));
      await WaitForAsync(async () => await TrigramFiltersBytesAsync(server) > 0, "filters rebuilt");
      Assert.AreEqual(FileCount + 1, await HitCountAsync(server));
      Assert.IsTrue((await ProjectDetailsAsync(server, root))["trigramFiltersEnabled"]!.GetValue<bool>());
    }
    finally {
      DeleteTree(root);
    }
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task TrigramFilters_EnabledBeforeTheProjectIsLoaded() {
    var root = CreateTree();
    try {
      await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
      // Sent before the project is known (e.g. when the client starts the server).
      await server.RequestAsync(ServerProtocol.SetProjectOptions(root, true));
      await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
        () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));
      Assert.AreEqual(FileCount, await HitCountAsync(server));
      await WaitForAsync(async () => await TrigramFiltersBytesAsync(server) > 0, "filters built");
      Assert.IsTrue((await ProjectDetailsAsync(server, root))["trigramFiltersEnabled"]!.GetValue<bool>());
    }
    finally {
      DeleteTree(root);
    }
  }

  [TestMethod]
  public async Task SetProjectOptions_RejectsRelativePaths() {
    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    var error = await Assert.ThrowsExactlyAsync<ServerErrorException>(() => server.RequestAsync(ServerProtocol.SetProjectOptions("relative\\path", false)));
    Assert.AreEqual(-32001, error.Code, "Recoverable error");
  }

  private static async Task WaitForAsync(Func<Task<bool>> condition, string what) {
    var stopwatch = Stopwatch.StartNew();
    while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)) {
      if (await condition())
        return;
      await Task.Delay(100);
    }
    Assert.Fail($"Timeout waiting for: {what}");
  }

  private static async Task<long> TrigramFiltersBytesAsync(JsonRpcServerClient server) {
    var response = await server.RequestAsync(ServerProtocol.GetServerStatistics());
    return response.Data["trigramFiltersBytes"]!.GetValue<long>();
  }

  private static async Task<long> HitCountAsync(JsonRpcServerClient server) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(Marker, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }

  private static async Task<System.Text.Json.Nodes.JsonObject> ProjectDetailsAsync(JsonRpcServerClient server, string root) {
    var response = await server.RequestAsync(ServerProtocol.GetProjectDetails(root));
    return response.Data;
  }
}
