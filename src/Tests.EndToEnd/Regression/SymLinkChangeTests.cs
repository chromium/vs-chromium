// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// Changes of the targets of symbolic links while the server runs: the directory watcher reports
/// the changes of a target under its real path only (and only if it is watched), the server maps
/// them to the paths of the links (and watches the targets outside of the projects if the option
/// "watchExternalLinkTargets" of the project is on), so that the index of the links is updated.
/// Inconclusive if symbolic links can't be created (Windows without Developer Mode or elevation).
/// </summary>
[TestClass]
public class SymLinkChangeTests {
  private const int NoLimit = 10_000_000;

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task FileLinkToATargetInsideTheProject() {
    using var tree = new TestTree();
    tree.WriteFile(Path.Combine("tools", "target.cc"), "// TARGET_ORIGINAL\n");
    tree.FileLink(Path.Combine("src", "link.cc"), Path.Combine("..", "tools", "target.cc"));
    await using var server = await tree.StartAsync();
    Assert.AreEqual(2, await HitCountAsync(server, "TARGET_ORIGINAL"), "The target and the link");

    tree.WriteFile(Path.Combine("tools", "target.cc"), "// TARGET_MODIFIED, longer\n");
    await WaitForHitsAsync(server, "TARGET_MODIFIED", 2);
    Assert.AreEqual(0, await HitCountAsync(server, "TARGET_ORIGINAL"), "No stale contents through the link");
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task FileLinkToATargetOutsideOfTheProject() {
    using var tree = new TestTree();
    var target = Path.Combine(tree.Outside, "target.cc");
    File.WriteAllText(target, "// OUTSIDE_ORIGINAL\n");
    tree.FileLink(Path.Combine("src", "link.cc"), target);
    await using var server = await tree.StartAsync(watchExternalLinkTargets: true);
    Assert.AreEqual(1, await HitCountAsync(server, "OUTSIDE_ORIGINAL"));
    var links = await tree.SymbolicLinksAsync(server);
    Assert.AreEqual(1, links["fileLinkCount"]!.GetValue<int>(), links.ToJsonString());
    Assert.AreEqual(1, links["externalTargetLinkCount"]!.GetValue<int>(), links.ToJsonString());
    Assert.AreEqual(1, links["externalTargetDirectoryCount"]!.GetValue<int>(), links.ToJsonString());
    Assert.IsTrue(links["externalTargetsWatched"]!.GetValue<bool>(), links.ToJsonString());
    Assert.AreEqual(1, links["watcherCount"]!.GetValue<int>(), links.ToJsonString());

    File.WriteAllText(target, "// OUTSIDE_MODIFIED, longer\n");
    await WaitForHitsAsync(server, "OUTSIDE_MODIFIED", 1);
    Assert.AreEqual(0, await HitCountAsync(server, "OUTSIDE_ORIGINAL"));

    // Restored (same contents as the start): the index follows.
    File.WriteAllText(target, "// OUTSIDE_ORIGINAL\n");
    await WaitForHitsAsync(server, "OUTSIDE_ORIGINAL", 1);
    Assert.AreEqual(0, await HitCountAsync(server, "OUTSIDE_MODIFIED"));
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task DirectoryLinkToADirectoryOutsideOfTheProject() {
    using var tree = new TestTree();
    var target = Path.Combine(tree.Outside, "linked");
    Directory.CreateDirectory(Path.Combine(target, "sub"));
    File.WriteAllText(Path.Combine(target, "sub", "a.cc"), "// LINKED_A_ORIGINAL\n");
    File.WriteAllText(Path.Combine(target, "b.cc"), "// LINKED_B\n");
    tree.DirectoryLink("dirlink", target);
    await using var server = await tree.StartAsync(watchExternalLinkTargets: true);
    Assert.AreEqual(1, await HitCountAsync(server, "LINKED_A_ORIGINAL"));

    // Modified, created and deleted files of the target: under the link path.
    File.WriteAllText(Path.Combine(target, "sub", "a.cc"), "// LINKED_A_MODIFIED, longer\n");
    File.WriteAllText(Path.Combine(target, "sub", "new.cc"), "// LINKED_NEW\n");
    File.Delete(Path.Combine(target, "b.cc"));
    await WaitForHitsAsync(server, "LINKED_A_MODIFIED", 1);
    await WaitForHitsAsync(server, "LINKED_NEW", 1);
    await WaitForHitsAsync(server, "LINKED_B", 0);
    var paths = await PathsAsync(server, "new.cc");
    CollectionAssert.Contains(paths, Path.Combine(tree.Root, "dirlink", "sub", "new.cc"));
  }

  /// <summary>
  /// The option "watchExternalLinkTargets" off (the default): a change of a target outside of the
  /// project is not applied while the server runs, but when it restarts (catch-up of the cache).
  /// </summary>
  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task TargetOutsideOfTheProjectNotWatchedByDefault() {
    using var tree = new TestTree();
    var target = Path.Combine(tree.Outside, "target.cc");
    File.WriteAllText(target, "// UNWATCHED_ORIGINAL\n");
    tree.FileLink(Path.Combine("src", "link.cc"), target);
    await using (var server = await tree.StartAsync(cacheDirectory: tree.CacheDirectory)) {
      Assert.AreEqual(1, await HitCountAsync(server, "UNWATCHED_ORIGINAL"));
      var links = await tree.SymbolicLinksAsync(server);
      Assert.AreEqual(1, links["externalTargetLinkCount"]!.GetValue<int>(), links.ToJsonString());
      Assert.AreEqual(1, links["externalTargetDirectoryCount"]!.GetValue<int>(), links.ToJsonString());
      Assert.IsFalse(links["externalTargetsWatched"]!.GetValue<bool>(), links.ToJsonString());
      Assert.AreEqual(0, links["watcherCount"]!.GetValue<int>(), links.ToJsonString());
      await WaitForAsync(async () => (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!["writeCount"]!
        .GetValue<int>() >= 1, "the cache is written");

      File.WriteAllText(target, "// UNWATCHED_MODIFIED, longer\n");
      await Task.Delay(TimeSpan.FromSeconds(3));
      Assert.AreEqual(1, await HitCountAsync(server, "UNWATCHED_ORIGINAL"), "Not watched: the link keeps its contents");
      Assert.AreEqual(0, await HitCountAsync(server, "UNWATCHED_MODIFIED"));
    }

    // Restarted: the file links are checked again (catch-up or full scan).
    await using var restarted = await tree.StartAsync(cacheDirectory: tree.CacheDirectory);
    await WaitForHitsAsync(restarted, "UNWATCHED_MODIFIED", 1);
    Assert.AreEqual(0, await HitCountAsync(restarted, "UNWATCHED_ORIGINAL"));
  }

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RetargetedFileLink() {
    using var tree = new TestTree();
    File.WriteAllText(Path.Combine(tree.Outside, "first.cc"), "// RETARGET_FIRST\n");
    File.WriteAllText(Path.Combine(tree.Outside, "second.cc"), "// RETARGET_SECOND, longer\n");
    tree.FileLink(Path.Combine("src", "link.cc"), Path.Combine(tree.Outside, "first.cc"));
    await using var server = await tree.StartAsync(watchExternalLinkTargets: true);
    Assert.AreEqual(1, await HitCountAsync(server, "RETARGET_FIRST"));

    // The link is replaced (its directory changes): the new target, then its changes.
    File.Delete(Path.Combine(tree.Root, "src", "link.cc"));
    tree.FileLink(Path.Combine("src", "link.cc"), Path.Combine(tree.Outside, "second.cc"));
    await WaitForHitsAsync(server, "RETARGET_SECOND", 1);
    Assert.AreEqual(0, await HitCountAsync(server, "RETARGET_FIRST"));
    File.WriteAllText(Path.Combine(tree.Outside, "second.cc"), "// RETARGET_SECOND_MODIFIED, even longer\n");
    await WaitForHitsAsync(server, "RETARGET_SECOND_MODIFIED", 1);
  }

  private static async Task<long> HitCountAsync(JsonRpcServerClient server, string text) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(text, maxResults: NoLimit, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }

  private static async Task<List<string>> PathsAsync(JsonRpcServerClient server, string pattern) {
    var response = await server.RequestAsync(ServerProtocol.SearchFilePaths(pattern, maxResults: NoLimit));
    return ServerProtocol.FlattenPathResults(response.Data).Select(x => x.FullPath).ToList();
  }

  private static async Task WaitForHitsAsync(JsonRpcServerClient server, string text, long expected) {
    var stopwatch = Stopwatch.StartNew();
    long count;
    while ((count = await HitCountAsync(server, text)) != expected) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Timeout waiting for {expected} hits of \"{text}\" ({count})");
      await Task.Delay(100);
    }
  }

  private static async Task WaitForAsync(Func<Task<bool>> condition, string description) {
    var stopwatch = Stopwatch.StartNew();
    while (!await condition()) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30), $"Timeout waiting for: {description}");
      await Task.Delay(100);
    }
  }

  /// <summary>A project and a directory outside of it (deleted when disposed).</summary>
  private sealed class TestTree : IDisposable {
    private readonly string _parent = Path.Combine(Path.GetTempPath(), "VsChromiumSymLinkChangeTest-" + Guid.NewGuid().ToString("N"));

    public TestTree() {
      Directory.CreateDirectory(Root);
      Directory.CreateDirectory(Outside);
      Directory.CreateDirectory(Path.Combine(Root, "src"));
      File.WriteAllText(Path.Combine(Root, "vs-chromium-project.txt"),
        "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n", new UTF8Encoding(false));
      WriteFile(Path.Combine("src", "file.cc"), "// A FILE\n");
    }

    public string Root => Path.Combine(_parent, "project");
    public string Outside => Path.Combine(_parent, "outside");
    public string CacheDirectory => Path.Combine(_parent, "cache");

    public void WriteFile(string relativePath, string text) {
      var path = Path.Combine(Root, relativePath);
      Directory.CreateDirectory(Path.GetDirectoryName(path)!);
      File.WriteAllText(path, text, new UTF8Encoding(false));
    }

    public void FileLink(string relativePath, string target) {
      try {
        File.CreateSymbolicLink(Path.Combine(Root, relativePath), target);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }
    }

    public void DirectoryLink(string relativePath, string target) {
      try {
        Directory.CreateSymbolicLink(Path.Combine(Root, relativePath), target);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }
    }

    /// <summary>
    /// Starts a server (with the index cache <paramref name="cacheDirectory"/>, a new one if null)
    /// and loads the project, with the option "watchExternalLinkTargets" (set before the load) and
    /// its watchers started.
    /// </summary>
    public async Task<JsonRpcServerClient> StartAsync(bool watchExternalLinkTargets = false, string? cacheDirectory = null) {
      var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath, cacheDirectory);
      if (watchExternalLinkTargets) {
        await server.RequestAsync(ServerProtocol.SetProjectOptions(Root, trigramFiltersEnabled: null, watchExternalLinkTargets: true));
      }
      await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
        () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(Root, "vs-chromium-project.txt"))));
      var stopwatch = Stopwatch.StartNew();
      while ((string)(await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["status"]! != "idle") {
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the status \"idle\"");
        await Task.Delay(100);
      }
      // Note: The map of the links (and the watchers) is built in the background after the scan.
      await WaitForAsync(async () => {
        var links = await SymbolicLinksAsync(server);
        return links["fileLinkCount"]!.GetValue<int>() + links["directoryLinkCount"]!.GetValue<int>() > 0 &&
               (!watchExternalLinkTargets || links["watcherCount"]!.GetValue<int>() >= links["externalTargetDirectoryCount"]!.GetValue<int>());
      }, "the map of the symbolic links");
      return server;
    }

    public async Task<System.Text.Json.Nodes.JsonNode> SymbolicLinksAsync(JsonRpcServerClient server) {
      var details = (await server.RequestAsync(ServerProtocol.GetProjectDetails(Root))).Data;
      return details["symbolicLinks"]!;
    }

    public void Dispose() {
      try {
        Directory.Delete(_parent, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        // Best effort
      }
    }
  }
}
