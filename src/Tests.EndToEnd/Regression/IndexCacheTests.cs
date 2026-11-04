// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// The persistent index cache: the tree and the contents of the files are written to the cache
/// after a load. A server started later with the same cache directory publishes the index of the
/// cache immediately ("index/loaded" with "fromCache", status "verifying"), then verifies it in the
/// background (scan, synthetic changes): the index must then be the same as an index built from
/// disk (cold load), including after changes made while no server is running. If the
/// configuration of the project changed, the tree of the cache is not used (only the contents of
/// unchanged files).
/// </summary>
[TestClass]
public class IndexCacheTests {
  public TestContext TestContext { get; set; } = null!;

  private const int NoLimit = 10_000_000;
  private const int DirectoryCount = 20;
  private const int FilesPerDirectory = 50;
  private const string VerifyDelayVariable = "VSCHROMIUM_TEST_VERIFY_DELAY_MS";
  private const string SaveDelaysVariable = "VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS";
  private const string HistoryIntervalVariable = "VSCHROMIUM_TEST_HISTORY_INTERVAL_MS";
  /// <summary>Delays of the saves of the cache ("settle,max" milliseconds) of the tests of the save policy.</summary>
  private const string ShortSaveDelays = "1500,5000";

  /// <summary>
  /// Files are older than the "racy" interval of the cache (files modified within 2 seconds of a
  /// load are not cached).
  /// </summary>
  private static readonly DateTime OldTime = DateTime.UtcNow.AddHours(-1);

  private static readonly string[] Queries = {
    "CACHE_MARKER_", "COMMON_TEXT", "caf\u00e9", "UTF16_MARKER", "LARGE_MARKER", "CHANGED_", "DURING_", "UNDETECTED___",
  };

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RestartPublishesTheCachedIndexThenVerifiesIt() {
    using var tree = new TestTree();
    var fileCount = tree.Generate();
    var coldResults = await ColdLoadAndWriteCacheAsync(tree);

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 5000);
    // The index of the cache, before its verification: no file read from disk.
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false, server.FirstLoaded.ToJsonString());
    Assert.AreEqual("verifying", await StatusAsync(server.Client));
    var statistics = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data;
    var cache = statistics["indexCache"]!;
    TestContext.WriteLine($"From the cache: {cache.ToJsonString()}");
    Assert.AreEqual(fileCount - 1, cache["lastLoadFilesFromCache"]!.GetValue<int>());
    Assert.AreEqual(0, cache["lastLoadFilesFromDisk"]!.GetValue<int>(), "No file read from disk");
    Assert.AreEqual(0, statistics["fileContentsArenas"]!["liveBytes"]!.GetValue<long>(), "No file in native arenas");
    // Searches work on the index of the cache. The large file (not cached) is in the tree, but its
    // contents are read by the verification.
    var cachedResults = await AllResultsAsync(server.Client);
    Assert.AreEqual(0, cachedResults["LARGE_MARKER"].Count);
    foreach (var query in Queries) {
      CollectionAssert.AreEqual(coldResults[query].Where(x => !x.Contains("large.txt:")).ToList(), cachedResults[query],
        $"Results of \"{query}\" from the cache (without the large file)");
    }
    CollectionAssert.AreEqual(coldResults["<paths>"], cachedResults["<paths>"]);
    // File extracts
    var match = ServerProtocol.FlattenCodeResults((await server.Client.RequestAsync(ServerProtocol.SearchCode("File 1/1 ", matchCase: true))).Data).Single();
    var extracts = (await server.Client.RequestAsync(ServerProtocol.GetFileExtracts(match.FullPath, match.Positions))).Data;
    StringAssert.Contains(extracts["extracts"]![0]!["text"]!.GetValue<string>(), "CACHE_MARKER_1_1");

    // After the verification: the same index as a cold load.
    await WaitForIdleAsync(server.Client);
    AssertSameResults(coldResults, await AllResultsAsync(server.Client));
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ChangesWhileTheServerIsStoppedAreIndexedByTheVerification() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);

    // Changes while no server runs.
    var sameSize = tree.FilePath(1, 1);
    var text = File.ReadAllText(sameSize);
    TestTree.WriteFile(sameSize, text.Replace("CACHE_MARKER_", "CHANGED_SAME_"), OldTime.AddMinutes(1));
    TestTree.WriteFile(tree.FilePath(2, 2), File.ReadAllText(tree.FilePath(2, 2)) + "// CHANGED_SIZE\n", OldTime);
    File.Delete(tree.FilePath(3, 3));
    Directory.Delete(Path.Combine(tree.Root, "dir004"), recursive: true);
    Directory.Move(Path.Combine(tree.Root, "dir006"), Path.Combine(tree.Root, "dir006_renamed"));
    TestTree.WriteFile(Path.Combine(tree.Root, "dir005", "new.cc"), "// CHANGED_NEW_FILE CACHE_MARKER_NEW\n", OldTime);
    Directory.CreateDirectory(Path.Combine(tree.Root, "newdir", "sub"));
    TestTree.WriteFile(Path.Combine(tree.Root, "newdir", "sub", "new.cc"), "// CHANGED_NEW_DIR\n", OldTime);
    TestTree.WriteFile(Path.Combine(tree.Root, "large.txt"), File.ReadAllText(Path.Combine(tree.Root, "large.txt")) + "// CHANGED_LARGE\n", OldTime);

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    // The index of the last session: none of the changes yet.
    Assert.AreEqual(0, await HitCountAsync(server.Client, "CHANGED_"));
    Assert.AreEqual(1, await HitCountAsync(server.Client, "File 3/3 "), "Deleted file still in the index");

    await WaitForIdleAsync(server.Client);
    var verified = await AllResultsAsync(server.Client);
    Assert.AreEqual(5, verified["CHANGED_"].Count, string.Join(", ", verified["CHANGED_"]));
    Assert.AreEqual(0, await HitCountAsync(server.Client, "File 3/3 "));
    // Same index as a cold load.
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    AssertSameResults(await AllResultsAsync(coldServer.Client), verified);
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ConfigurationChangeDoesNotUseTheTreeOfTheCache() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    // Another ignore rule: another version hash of the project.
    TestTree.WriteFile(tree.ProjectFile,
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\nutf8*.txt\n", OldTime);

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsFalse(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false, "Scan first (phase 1)");
    var cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
    Assert.IsTrue(cache["lastLoadFilesFromCache"]!.GetValue<int>() > 1000, "The contents of the cache are used");
    await WaitForIdleAsync(server.Client);
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    var results = await AllResultsAsync(server.Client);
    Assert.AreEqual(1, results["caf\u00e9"].Count, "utf8*.txt not searchable");
    AssertSameResults(await AllResultsAsync(coldServer.Client), results);
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ChangesDuringTheVerificationAreIndexed() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    // While verifying: a new file, a modified file.
    File.WriteAllText(Path.Combine(tree.Root, "dir001", "during.cc"), "// DURING_VERIFY_NEW\n");
    File.AppendAllText(tree.FilePath(7, 7), "// DURING_VERIFY_EDIT\n");

    await WaitForIdleAsync(server.Client);
    var stopwatch = Stopwatch.StartNew();
    while (await HitCountAsync(server.Client, "DURING_VERIFY_") != 2) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the changes");
      await Task.Delay(100);
    }
    await WaitForIdleAsync(server.Client);
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    AssertSameResults(await AllResultsAsync(coldServer.Client), await AllResultsAsync(server.Client));
  }

  /// <summary>
  /// A file modified while no server runs, with the same size and the same last write time, can't
  /// be detected (same rule as the index of git): "Rebuild index" (tree/refresh), even during the
  /// verification, deletes the cache and reads all the files from disk.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RebuildIndexDuringTheVerificationDoesNotUseTheCache() {
    using var tree = new TestTree();
    var fileCount = tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    var path = tree.FilePath(1, 1);
    TestTree.WriteFile(path, File.ReadAllText(path).Replace("CACHE_MARKER_", "UNDETECTED___"), OldTime);

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 20000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    Assert.AreEqual(0, await HitCountAsync(server.Client, "UNDETECTED___"), "Same time and size: the cached contents are used");

    await server.Client.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => server.Client.RequestAsync(ServerProtocol.RefreshTree()));
    await WaitForIdleAsync(server.Client, TimeSpan.FromSeconds(15)); // Before the end of the delay of the verification
    Assert.AreEqual(1, await HitCountAsync(server.Client, "UNDETECTED___"));
    var cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
    Assert.AreEqual(0, cache["lastLoadFilesFromCache"]!.GetValue<int>(), cache.ToJsonString());
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    AssertSameResults(await AllResultsAsync(coldServer.Client), await AllResultsAsync(server.Client));

    // The cache is written again (all the text files are new, except the large file).
    cache = await WaitForCacheWriteAsync(server.Client, 1);
    Assert.AreEqual(fileCount - 2, cache["lastWriteNewFileCount"]!.GetValue<int>(), cache.ToJsonString());
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task PauseDuringTheVerification() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    File.Delete(tree.FilePath(3, 3));

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await server.Client.RequestAsync(ServerProtocol.PauseIndexing());
    await WaitForStatusAsync(server.Client, "paused");
    await Task.Delay(4000); // The verification doesn't run while paused.
    Assert.AreEqual("paused", await StatusAsync(server.Client));
    Assert.AreEqual(1, await HitCountAsync(server.Client, "File 3/3 "));
    // Resume: full rescan.
    await server.Client.RequestAsync(ServerProtocol.ResumeIndexing());
    await WaitForIdleAsync(server.Client);
    var stopwatch = Stopwatch.StartNew();
    while (await HitCountAsync(server.Client, "File 3/3 ") != 0) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the rescan");
      await Task.Delay(100);
    }
    await WaitForIdleAsync(server.Client);
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    AssertSameResults(await AllResultsAsync(coldServer.Client), await AllResultsAsync(server.Client));
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task SecondProjectRegisteredDuringTheVerification() {
    using var tree = new TestTree();
    using var other = new TestTree();
    tree.Generate();
    other.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    File.Delete(tree.FilePath(3, 3));

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    // The registration of the other project cancels the verification, which runs again after.
    await server.Client.RequestAsync(ServerProtocol.RegisterFile(other.ProjectFile));
    var stopwatch = Stopwatch.StartNew();
    while (await StatusAsync(server.Client) != "idle" || await HitCountAsync(server.Client, "File 3/3 ") != 1) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the index of both projects");
      await Task.Delay(100);
    }

    // Same index as a cold load of both projects.
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await coldServer.Client.RequestAsync(ServerProtocol.RegisterFile(other.ProjectFile));
    stopwatch.Restart();
    while (await StatusAsync(coldServer.Client) != "idle" || await HitCountAsync(coldServer.Client, "File 3/3 ") != 1) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the cold index of both projects");
      await Task.Delay(100);
    }
    AssertSameResults(await AllResultsAsync(coldServer.Client), await AllResultsAsync(server.Client));
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task SecondServerUsesTheCacheReadOnly() {
    using var tree = new TestTree();
    tree.Generate();
    await using var first = await StartAndLoadAsync(tree, tree.CacheDirectory);
    await WaitForCacheWriteAsync(first.Client, 1);
    var firstResults = await AllResultsAsync(first.Client);

    // The first server holds the lock of the cache: the second one publishes the index of the
    // cache, verifies it, and doesn't write the cache.
    await using var second = await StartAndLoadAsync(tree, tree.CacheDirectory);
    Assert.IsTrue(second.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await WaitForIdleAsync(second.Client);
    AssertSameResults(firstResults, await AllResultsAsync(second.Client));
    await Task.Delay(1000);
    var cache = (await second.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
    Assert.AreEqual(1, cache["readOnlyProjectCount"]!.GetValue<int>());
    Assert.AreEqual(0, cache["writeCount"]!.GetValue<int>());
  }

  /// <summary>
  /// The state of the cache in "index/statistics" (status displays): saved after a load, "dirty"
  /// after an edit (the changed files path doesn't write the cache), saved again after a change of
  /// the tree (new file: incremental update with file system changes, which writes the cache).
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task IndexStatisticsShowTheStateOfTheCache() {
    using var tree = new TestTree();
    tree.Generate();
    await using var server = await StartAndLoadAsync(tree, tree.CacheDirectory, saveDelays: ShortSaveDelays);
    await WaitForIdleAsync(server.Client);
    await WaitForCacheWriteAsync(server.Client, 1);
    var cache = await WaitForIndexCacheAsync(server.Client, x => !x["saving"]!.GetValue<bool>(), "saved");
    Assert.IsTrue(cache["enabled"]!.GetValue<bool>());
    Assert.IsNotNull(cache["lastSavedUtc"], cache.ToJsonString());
    Assert.IsTrue(cache["sizeOnDiskBytes"]!.GetValue<long>() > 1000 * 500, cache.ToJsonString());
    Assert.IsFalse(cache["dirty"]!.GetValue<bool>(), cache.ToJsonString());

    // An edit: the index changes ("dirty"), then the cache is saved once the changes settle.
    File.AppendAllText(tree.FilePath(7, 7), "// EDITED_FILE\n");
    await WaitForIndexCacheAsync(server.Client, x => x["dirty"]!.GetValue<bool>(), "dirty");
    Assert.AreEqual(1, await HitCountAsync(server.Client, "EDITED_FILE"));
    await WaitForIndexCacheAsync(server.Client, x => !x["dirty"]!.GetValue<bool>() && !x["saving"]!.GetValue<bool>(), "saved again");
    await WaitForCacheWriteAsync(server.Client, 2);
  }

  /// <summary>
  /// Edited files are saved to the cache (with the metadata of their read: the scan is not updated
  /// by edits), and loaded from the cache after a restart.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task EditedFilesAreLoadedFromTheCacheAfterARestart() {
    using var tree = new TestTree();
    var fileCount = tree.Generate();
    await using (var server = await StartAndLoadAsync(tree, tree.CacheDirectory, saveDelays: ShortSaveDelays)) {
      await WaitForIdleAsync(server.Client);
      await WaitForCacheWriteAsync(server.Client, 1);
      // An edit (old time: not "racy").
      var path = tree.FilePath(7, 7);
      TestTree.WriteFile(path, File.ReadAllText(path) + "// EDITED_FILE\n", OldTime.AddMinutes(2));
      var stopwatch = Stopwatch.StartNew();
      while (await HitCountAsync(server.Client, "EDITED_FILE") != 1) {
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the edit");
        await Task.Delay(100);
      }
      var cache = await WaitForCacheWriteAsync(server.Client, 2);
      Assert.AreEqual(fileCount - 1, cache["lastWriteEntryCount"]!.GetValue<int>(), cache.ToJsonString());
    }

    await using (var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000)) {
      Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
      var cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
      Assert.AreEqual(fileCount - 1, cache["lastLoadFilesFromCache"]!.GetValue<int>(), cache.ToJsonString());
      Assert.AreEqual(1, await HitCountAsync(server.Client, "EDITED_FILE"), "The edited file is in the cache");
      // The verification only reads the large file (the edited file is unchanged since it was read).
      await WaitForIdleAsync(server.Client);
      cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
      Assert.AreEqual(1, cache["lastLoadFilesFromDisk"]!.GetValue<int>(), cache.ToJsonString());
    }
  }

  /// <summary>
  /// Deleted and edited files are dead bytes of the arena files of the cache; a save compacts them
  /// (live slots copied to new arena files); a restart loads the compacted cache.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task DeadBytesAreCompacted() {
    using var tree = new TestTree();
    tree.Generate();
    Dictionary<string, List<string>> results;
    await using (var server = await StartAndLoadAsync(tree, tree.CacheDirectory, saveDelays: ShortSaveDelays)) {
      await WaitForIdleAsync(server.Client);
      await WaitForCacheWriteAsync(server.Client, 1);
      var state = await WaitForIndexCacheAsync(server.Client, x => !x["saving"]!.GetValue<bool>(), "saved");
      Assert.AreEqual(0, state["deadBytes"]!.GetValue<long>(), state.ToJsonString());

      // 20% of the files deleted: dead bytes.
      for (var d = 0; d < DirectoryCount; d++) {
        for (var f = 0; f < FilesPerDirectory; f += 5) {
          File.Delete(tree.FilePath(d, f));
        }
      }
      await WaitForCacheWriteAsync(server.Client, 2);
      state = await WaitForIndexCacheAsync(server.Client, x => !x["saving"]!.GetValue<bool>() && !x["dirty"]!.GetValue<bool>(), "saved");
      var deadAfterDeletions = state["deadBytes"]!.GetValue<long>();
      Assert.IsTrue(deadAfterDeletions > 0, state.ToJsonString());

      // 20% of the files edited: more than 25% of the arena bytes would be dead: the save compacts.
      for (var d = 0; d < DirectoryCount; d++) {
        for (var f = 1; f < FilesPerDirectory; f += 5) {
          TestTree.WriteFile(tree.FilePath(d, f), File.ReadAllText(tree.FilePath(d, f)).Replace("COMMON_TEXT", "EDITED_TEXT"), OldTime.AddMinutes(1));
        }
      }
      var cache = await WaitForCacheWriteAsync(server.Client, 3);
      TestContext.WriteLine($"Compaction: {cache.ToJsonString()}");
      Assert.IsTrue(cache["lastWriteCompactedArenaCount"]!.GetValue<int>() >= 1, cache.ToJsonString());
      state = await WaitForIndexCacheAsync(server.Client, x => !x["saving"]!.GetValue<bool>() && !x["dirty"]!.GetValue<bool>(), "saved");
      Assert.IsTrue(state["deadBytes"]!.GetValue<long>() < deadAfterDeletions, state.ToJsonString());
      results = await AllResultsAsync(server.Client);
      Assert.IsTrue(results["COMMON_TEXT"].Count > 0 && (await HitCountAsync(server.Client, "EDITED_TEXT")) > 0);
    }

    // Restart: the compacted cache.
    await using (var server = await StartAndLoadAsync(tree, verifyDelayMs: 3000)) {
      Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
      var cached = await AllResultsAsync(server.Client);
      foreach (var (query, expected) in results) {
        CollectionAssert.AreEqual(expected.Where(x => !x.Contains("large.txt:")).ToList(), cached[query], $"Results of \"{query}\" from the cache");
      }
      await WaitForIdleAsync(server.Client);
      AssertSameResults(results, await AllResultsAsync(server.Client));
    }
  }

  /// <summary>
  /// A restart without changes: the verified index is the same as the cache (the large file read
  /// by the verification is not cached anyway): nothing to save.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task NoSaveAfterAVerificationWithoutChanges() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    var indexTime = File.GetLastWriteTimeUtc(Directory.GetFiles(tree.CacheDirectory, "index.bin", SearchOption.AllDirectories).Single());

    await using var server = await StartAndLoadAsync(tree, tree.CacheDirectory, saveDelays: ShortSaveDelays);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await WaitForIdleAsync(server.Client);
    await Task.Delay(3000); // More than the settle delay
    var cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
    Assert.AreEqual(0, cache["writeCount"]!.GetValue<int>(), cache.ToJsonString());
    var state = (await server.Client.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["indexCache"]!;
    Assert.IsFalse(state["dirty"]!.GetValue<bool>(), state.ToJsonString());
    Assert.AreEqual(indexTime, File.GetLastWriteTimeUtc(Directory.GetFiles(tree.CacheDirectory, "index.bin", SearchOption.AllDirectories).Single()));
  }

  /// <summary>
  /// No save at shutdown: a server with unsaved changes stops without saving them (closing the
  /// client is not delayed); they are read by the verification at the next start.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task NoSaveAtShutdown() {
    using var tree = new TestTree();
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);

    var server = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(server.Client);
    var path = tree.FilePath(7, 7);
    TestTree.WriteFile(path, File.ReadAllText(path) + "// EDITED_FILE\n", OldTime.AddMinutes(2));
    await WaitForIndexCacheAsync(server.Client, x => x["dirty"]!.GetValue<bool>(), "dirty");
    var stopwatch = Stopwatch.StartNew();
    await server.DisposeAsync();
    Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"Stopped in {stopwatch.Elapsed.TotalMilliseconds:n0} ms");

    await using var restarted = await StartAndLoadAsync(tree, verifyDelayMs: 3000);
    Assert.IsTrue(restarted.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    Assert.AreEqual(0, await HitCountAsync(restarted.Client, "EDITED_FILE"), "Not saved");
    await WaitForIdleAsync(restarted.Client);
    Assert.AreEqual(1, await HitCountAsync(restarted.Client, "EDITED_FILE"), "Read by the verification");
  }

  /// <summary>
  /// "server/history": samples taken from the start of the server (every 200 ms here, 10 s by
  /// default), returned in columns, all of them or the ones after a time.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ServerHistoryIsSampledFromTheStart() {
    using var tree = new TestTree();
    tree.Generate();
    await using var server = await StartAndLoadAsync(tree, tree.CacheDirectory, historyIntervalMs: 200);
    await WaitForIdleAsync(server.Client);
    await WaitForCacheWriteAsync(server.Client, 1);
    var statistics = (await server.Client.RequestAsync(ServerProtocol.GetIndexStatistics())).Data;
    var fileCount = statistics["fileCount"]!.GetValue<int>();
    Assert.IsTrue(fileCount > 1000);

    // Samples of the loaded index, after the save.
    var stopwatch = Stopwatch.StartNew();
    JsonObject history;
    while (true) {
      history = (await server.Client.RequestAsync(ServerProtocol.GetServerHistory())).Data;
      var files = history["fileCount"]!.AsArray();
      var sizes = history["cacheSizeOnDiskBytes"]!.AsArray();
      if (files.Count >= 3 && files[^1]!.GetValue<int>() == fileCount && sizes[^1]!.GetValue<long>() > 0)
        break;
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(30), history.ToJsonString());
      await Task.Delay(200);
    }
    TestContext.WriteLine(history.ToJsonString());
    Assert.AreEqual(200, history["intervalMs"]!.GetValue<int>());
    Assert.AreEqual(720, history["capacity"]!.GetValue<int>());
    var times = history["timesUnixMs"]!.AsArray().Select(x => x!.GetValue<long>()).ToList();
    var count = times.Count;
    foreach (var column in new[] { "gcHeapBytes", "gcTotalAllocatedBytes", "nativeAllocatedBytes", "workingSetBytes",
               "privateBytes", "cpuTotalMs", "fileCount", "searchableFileCount", "indexedTextBytes", "cacheSizeOnDiskBytes", "status" }) {
      Assert.AreEqual(count, history[column]!.AsArray().Count, column);
    }
    CollectionAssert.AreEqual(times.OrderBy(x => x).ToList(), times, "Oldest first");
    var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    Assert.IsTrue(times[^1] <= now && times[^1] > now - 60_000, $"{times[^1]} {now}");
    Assert.IsTrue(history["workingSetBytes"]![count - 1]!.GetValue<long>() > 0);
    Assert.IsTrue(history["cpuTotalMs"]![count - 1]!.GetValue<double>() > 0);
    Assert.IsTrue(history["indexedTextBytes"]![count - 1]!.GetValue<long>() > 0);
    Assert.AreEqual(statistics["searchableFileCount"]!.GetValue<int>(), history["searchableFileCount"]![count - 1]!.GetValue<int>());
    Assert.AreEqual(statistics["indexedTextBytes"]!.GetValue<long>(), history["indexedTextBytes"]![count - 1]!.GetValue<long>());
    Assert.AreEqual("idle", history["status"]![count - 1]!.GetValue<string>());
    // The first samples were taken before the load (at the start of the server).
    Assert.AreEqual(0, history["fileCount"]![0]!.GetValue<int>());

    // Incremental: the samples after the last one received.
    await Task.Delay(500);
    var since = (await server.Client.RequestAsync(ServerProtocol.GetServerHistory(times[^1]))).Data;
    var newTimes = since["timesUnixMs"]!.AsArray().Select(x => x!.GetValue<long>()).ToList();
    Assert.IsTrue(newTimes.Count >= 1, since.ToJsonString());
    Assert.IsTrue(newTimes.All(x => x > times[^1]));
    Assert.AreEqual(newTimes.Count, since["status"]!.AsArray().Count);
  }

  private static async Task<JsonNode> WaitForIndexCacheAsync(JsonRpcServerClient server, Func<JsonNode, bool> predicate, string description) {
    var stopwatch = Stopwatch.StartNew();
    while (true) {
      var cache = (await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["indexCache"]!;
      if (predicate(cache))
        return cache;
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Timeout waiting for the cache state \"{description}\": {cache.ToJsonString()}");
      await Task.Delay(100);
    }
  }

  /// <summary>A generated project and its cache directory (deleted when disposed).</summary>
  private sealed class TestTree : IDisposable {
    public string Root { get; } = NewDirectory("VsChromiumIndexCacheTest-");
    public string CacheDirectory { get; } = NewDirectory("VsChromiumIndexCacheTestCache-");
    public string ProjectFile => Path.Combine(Root, "vs-chromium-project.txt");

    public string FilePath(int directory, int file) => Path.Combine(Root, $"dir{directory:D3}", $"file{file:D3}.cc");

    public static void WriteFile(string path, string text, DateTime lastWriteTimeUtc, Encoding? encoding = null) {
      File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
      File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
    }

    /// <summary>
    /// Generates the tree (old files): text files, a binary file, UTF-8 (with and without BOM) and
    /// UTF-16 files, a large file (not cached), empty directories. Returns the number of files.
    /// </summary>
    public int Generate() {
      WriteFile(ProjectFile, "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n", OldTime);
      for (var d = 0; d < DirectoryCount; d++) {
        Directory.CreateDirectory(Path.Combine(Root, $"dir{d:D3}"));
        for (var f = 0; f < FilesPerDirectory; f++) {
          var sb = new StringBuilder();
          sb.Append("// File ").Append(d).Append('/').Append(f).Append(" CACHE_MARKER_").Append(d).Append('_').Append(f).Append('\n');
          for (var line = 0; line < 20 + f; line++) {
            sb.Append("int function_").Append(line).Append("() { return COMMON_TEXT; }\n");
          }
          WriteFile(FilePath(d, f), sb.ToString(), OldTime);
        }
      }
      Directory.CreateDirectory(Path.Combine(Root, "empty", "nested"));
      WriteFile(Path.Combine(Root, "utf8.txt"), "caf\u00e9 COMMON_TEXT\n", OldTime);
      WriteFile(Path.Combine(Root, "utf8bom.txt"), "caf\u00e9 with BOM COMMON_TEXT\n", OldTime, new UTF8Encoding(true));
      WriteFile(Path.Combine(Root, "utf16.txt"), "UTF16_MARKER caf\u00e9 COMMON_TEXT\n", OldTime, Encoding.Unicode);
      File.WriteAllBytes(Path.Combine(Root, "image.bin"), new byte[] { 0, 1, 2, 3, 0, 0, 0, 0, 255, 254 });
      File.SetLastWriteTimeUtc(Path.Combine(Root, "image.bin"), OldTime);
      var large = new StringBuilder("// LARGE_MARKER\n");
      while (large.Length < 600 * 1024) {
        large.Append("int large_function() { return COMMON_TEXT; }\n");
      }
      WriteFile(Path.Combine(Root, "large.txt"), large.ToString(), OldTime);
      return 1 + DirectoryCount * FilesPerDirectory + 5;
    }

    public void Dispose() {
      DeleteDirectory(Root);
      DeleteDirectory(CacheDirectory);
    }
  }

  private static string NewDirectory(string prefix) {
    var path = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
  }

  private static void DeleteDirectory(string path) {
    try {
      Directory.Delete(path, recursive: true);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      // Best effort
    }
  }

  /// <summary>A started server, and the first "index/loaded" notification.</summary>
  private sealed class LoadedServer(JsonRpcServerClient client, JsonObject firstLoaded) : IAsyncDisposable {
    public JsonRpcServerClient Client { get; } = client;
    public JsonObject FirstLoaded { get; } = firstLoaded;
    public ValueTask DisposeAsync() => Client.DisposeAsync();
  }

  private static Task<LoadedServer> StartAndLoadAsync(TestTree tree, int verifyDelayMs = 0) {
    return StartAndLoadAsync(tree, tree.CacheDirectory, verifyDelayMs);
  }

  private static async Task<LoadedServer> StartAndLoadAsync(TestTree tree, string? cacheDirectory, int verifyDelayMs = 0,
                                                            string? saveDelays = null, int historyIntervalMs = 0) {
    var environment = new Dictionary<string, string?> {
      [VerifyDelayVariable] = verifyDelayMs > 0 ? verifyDelayMs.ToString() : null,
      [SaveDelaysVariable] = saveDelays,
      [HistoryIntervalVariable] = historyIntervalMs > 0 ? historyIntervalMs.ToString() : null,
    };
    var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath, cacheDirectory, environment);
    var loaded = await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => server.RequestAsync(ServerProtocol.RegisterFile(tree.ProjectFile)));
    return new LoadedServer(server, loaded);
  }

  /// <summary>
  /// Cold load (no cache), waits for the cache write, and returns the results; the server is
  /// stopped.
  /// </summary>
  private static async Task<Dictionary<string, List<string>>> ColdLoadAndWriteCacheAsync(TestTree tree) {
    await using var server = await StartAndLoadAsync(tree);
    Assert.IsFalse(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await WaitForIdleAsync(server.Client);
    var cache = await WaitForCacheWriteAsync(server.Client, 1);
    Assert.AreEqual(0, cache["lastLoadFilesFromCache"]!.GetValue<int>());
    return await AllResultsAsync(server.Client);
  }

  private static async Task<string> StatusAsync(JsonRpcServerClient server) {
    return (string)(await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["status"]!;
  }

  private static async Task WaitForStatusAsync(JsonRpcServerClient server, string status, TimeSpan? timeout = null) {
    var stopwatch = Stopwatch.StartNew();
    while (await StatusAsync(server) != status) {
      Assert.IsTrue(stopwatch.Elapsed < (timeout ?? TimeSpan.FromSeconds(60)), $"Timeout waiting for the status \"{status}\"");
      await Task.Delay(100);
    }
  }

  private static Task WaitForIdleAsync(JsonRpcServerClient server, TimeSpan? timeout = null) {
    return WaitForStatusAsync(server, "idle", timeout);
  }

  /// <summary>Waits until the cache has been written <paramref name="count"/> times.</summary>
  private static async Task<JsonNode> WaitForCacheWriteAsync(JsonRpcServerClient server, int count) {
    var stopwatch = Stopwatch.StartNew();
    while (true) {
      var cache = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
      if (cache["writeCount"]!.GetValue<int>() >= count)
        return cache;
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Timeout waiting for the cache write: {cache.ToJsonString()}");
      await Task.Delay(100);
    }
  }

  private static async Task<long> HitCountAsync(JsonRpcServerClient server, string text) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(text, maxResults: NoLimit, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }

  /// <summary>
  /// The results of <see cref="Queries"/> (sorted "path:position:length" strings), and the paths
  /// of the files.
  /// </summary>
  private static async Task<Dictionary<string, List<string>>> AllResultsAsync(JsonRpcServerClient server) {
    var result = new Dictionary<string, List<string>>();
    foreach (var query in Queries) {
      var response = await server.RequestAsync(ServerProtocol.SearchCode(query, maxResults: NoLimit, matchCase: true));
      result[query] = ServerProtocol.FlattenCodeResults(response.Data)
        .SelectMany(file => file.Positions.Select(p => $"{file.FullPath}:{p.Position}:{p.Length}"))
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToList();
    }
    var paths = await server.RequestAsync(ServerProtocol.SearchFilePaths("*", maxResults: NoLimit));
    result["<paths>"] = ServerProtocol.FlattenPathResults(paths.Data)
      .Select(file => file.FullPath)
      .OrderBy(x => x, StringComparer.Ordinal)
      .ToList();
    return result;
  }

  private static void AssertSameResults(Dictionary<string, List<string>> expected, Dictionary<string, List<string>> actual) {
    foreach (var (query, results) in expected) {
      CollectionAssert.AreEqual(results, actual[query], $"Results of \"{query}\"");
    }
    Assert.IsNotEmpty(expected["CACHE_MARKER_"]);
    Assert.IsNotEmpty(expected["UTF16_MARKER"]);
    Assert.IsNotEmpty(expected["LARGE_MARKER"]);
  }
}
