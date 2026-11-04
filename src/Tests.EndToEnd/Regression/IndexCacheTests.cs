// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// The persistent index cache (phase 1): the contents of the files are written to the cache after
/// a load, and a server started later (same cache directory) loads the unchanged files from the
/// cache. The index must be the same as an index built from disk (cold load), including after
/// changes made while no server is running.
/// </summary>
[TestClass]
public class IndexCacheTests {
  public TestContext TestContext { get; set; } = null!;

  private const int NoLimit = 10_000_000;
  private const int DirectoryCount = 20;
  private const int FilesPerDirectory = 50;

  /// <summary>
  /// Files are older than the "racy" interval of the cache (files modified within 2 seconds of a
  /// load are not cached).
  /// </summary>
  private static readonly DateTime OldTime = DateTime.UtcNow.AddHours(-1);

  private static readonly string[] Queries = { "CACHE_MARKER_", "COMMON_TEXT", "caf\u00e9", "UTF16_MARKER", "LARGE_MARKER", "CHANGED_" };

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RestartLoadsTheFilesFromTheCache() {
    var root = NewDirectory("VsChromiumIndexCacheTest-");
    var cacheDirectory = NewDirectory("VsChromiumIndexCacheTestCache-");
    try {
      var fileCount = GenerateTree(root);
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");

      // Cold load: everything read from disk, then written to the cache.
      Dictionary<string, List<string>> coldResults;
      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        var cache = await WaitForCacheWriteAsync(server, 1);
        Assert.AreEqual(0, cache["lastLoadFilesFromCache"]!.GetValue<int>());
        Assert.AreEqual(fileCount, cache["lastLoadFilesFromDisk"]!.GetValue<int>());
        // All files except the large one.
        Assert.AreEqual(fileCount - 1, cache["lastWriteEntryCount"]!.GetValue<int>());
        coldResults = await AllResultsAsync(server);
      }

      // Restart: the files are loaded from the cache (mapped), except the large file.
      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        var statistics = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data;
        var cache = statistics["indexCache"]!;
        TestContext.WriteLine($"Restart: {cache.ToJsonString()}");
        Assert.AreEqual(fileCount - 1, cache["lastLoadFilesFromCache"]!.GetValue<int>());
        Assert.AreEqual(1, cache["lastLoadFilesFromDisk"]!.GetValue<int>(), "Only the large file is read");
        var arenas = statistics["fileContentsArenas"]!;
        Assert.IsTrue(arenas["cachedFileBytes"]!.GetValue<long>() > fileCount * 1000L, arenas.ToJsonString());
        Assert.AreEqual(0, arenas["liveBytes"]!.GetValue<long>(), "No file in native arenas");
        AssertSameResults(coldResults, await AllResultsAsync(server));
        // Nothing new to write.
        cache = await WaitForCacheWriteAsync(server, 1);
        Assert.AreEqual(0, cache["lastWriteNewFileCount"]!.GetValue<int>());
      }
    }
    finally {
      DeleteDirectory(root);
      DeleteDirectory(cacheDirectory);
    }
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ChangesWhileTheServerIsStoppedAreIndexed() {
    var root = NewDirectory("VsChromiumIndexCacheTest-");
    var cacheDirectory = NewDirectory("VsChromiumIndexCacheTestCache-");
    try {
      var fileCount = GenerateTree(root);
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");
      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        await WaitForCacheWriteAsync(server, 1);
      }

      // Changes while no server runs: same size (other time), other size (same time), deleted,
      // new files, new directory.
      var sameSize = FilePath(root, 1, 1);
      var text = File.ReadAllText(sameSize);
      var changed = text.Replace("CACHE_MARKER_", "CHANGED_SAME_");
      Assert.AreEqual(text.Length, changed.Length);
      WriteFile(sameSize, changed, OldTime.AddMinutes(1));
      WriteFile(FilePath(root, 2, 2), File.ReadAllText(FilePath(root, 2, 2)) + "// CHANGED_SIZE\n", OldTime);
      File.Delete(FilePath(root, 3, 3));
      Directory.Delete(Path.Combine(root, "dir004"), recursive: true);
      WriteFile(Path.Combine(root, "dir005", "new.cc"), "// CHANGED_NEW_FILE CACHE_MARKER_NEW\n", OldTime);
      Directory.CreateDirectory(Path.Combine(root, "newdir"));
      WriteFile(Path.Combine(root, "newdir", "new.cc"), "// CHANGED_NEW_DIR\n", OldTime);

      Dictionary<string, List<string>> cachedResults;
      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        var cache = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
        TestContext.WriteLine($"Restart after changes: {cache.ToJsonString()}");
        // The large file, the 2 modified files and the 2 new files are read from disk.
        Assert.AreEqual(5, cache["lastLoadFilesFromDisk"]!.GetValue<int>());
        cachedResults = await AllResultsAsync(server);
      }

      // Same index as a cold load (new cache).
      await using (var coldServer = await StartAndLoadAsync(projectFile, cacheDirectory: null)) {
        var cache = (await coldServer.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
        Assert.AreEqual(0, cache["lastLoadFilesFromCache"]!.GetValue<int>());
        AssertSameResults(await AllResultsAsync(coldServer), cachedResults);
      }
      Assert.AreEqual(4, cachedResults["CHANGED_"].Count, string.Join(", ", cachedResults["CHANGED_"]));
      Assert.IsTrue(cachedResults["CHANGED_"].Any(x => x.StartsWith(sameSize + ":")), "File modified with the same size");
    }
    finally {
      DeleteDirectory(root);
      DeleteDirectory(cacheDirectory);
    }
  }

  /// <summary>
  /// A file modified while no server runs, with the same size and the same last write time, can't
  /// be detected (same rule as the index of git): "Rebuild index" (tree/refresh) deletes the cache
  /// and reads all the files from disk.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RebuildIndexDoesNotUseTheCache() {
    var root = NewDirectory("VsChromiumIndexCacheTest-");
    var cacheDirectory = NewDirectory("VsChromiumIndexCacheTestCache-");
    try {
      var fileCount = GenerateTree(root);
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");
      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        await WaitForCacheWriteAsync(server, 1);
      }

      var path = FilePath(root, 1, 1);
      var text = File.ReadAllText(path);
      WriteFile(path, text.Replace("CACHE_MARKER_", "UNDETECTED___"), OldTime);

      await using (var server = await StartAndLoadAsync(projectFile, cacheDirectory)) {
        Assert.AreEqual(0, await HitCountAsync(server, "UNDETECTED___"), "Same time and size: the cached contents are used");

        await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
          () => server.RequestAsync(ServerProtocol.RefreshTree()));
        var stopwatch = Stopwatch.StartNew();
        while (await HitCountAsync(server, "UNDETECTED___") != 1 && stopwatch.Elapsed < TimeSpan.FromSeconds(60)) {
          await Task.Delay(100);
        }
        Assert.AreEqual(1, await HitCountAsync(server, "UNDETECTED___"));
        var cache = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
        Assert.AreEqual(0, cache["lastLoadFilesFromCache"]!.GetValue<int>(), cache.ToJsonString());

        // The cache is written again (all the text files are new, except the large file).
        cache = await WaitForCacheWriteAsync(server, 2);
        Assert.AreEqual(fileCount - 2, cache["lastWriteNewFileCount"]!.GetValue<int>());
      }
    }
    finally {
      DeleteDirectory(root);
      DeleteDirectory(cacheDirectory);
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

  private static string FilePath(string root, int directory, int file) =>
    Path.Combine(root, $"dir{directory:D3}", $"file{file:D3}.cc");

  private static void WriteFile(string path, string text, DateTime lastWriteTimeUtc, Encoding? encoding = null) {
    File.WriteAllText(path, text, encoding ?? new UTF8Encoding(false));
    File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
  }

  /// <summary>
  /// Generates the tree (old files): text files, a binary file, UTF-8 (with and without BOM) and
  /// UTF-16 files, a large file (not cached). Returns the number of files.
  /// </summary>
  private static int GenerateTree(string root) {
    WriteFile(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n", OldTime);
    for (var d = 0; d < DirectoryCount; d++) {
      Directory.CreateDirectory(Path.Combine(root, $"dir{d:D3}"));
      for (var f = 0; f < FilesPerDirectory; f++) {
        var sb = new StringBuilder();
        sb.Append("// File ").Append(d).Append('/').Append(f).Append(" CACHE_MARKER_").Append(d).Append('_').Append(f).Append('\n');
        for (var line = 0; line < 20 + f; line++) {
          sb.Append("int function_").Append(line).Append("() { return COMMON_TEXT; }\n");
        }
        WriteFile(FilePath(root, d, f), sb.ToString(), OldTime);
      }
    }
    WriteFile(Path.Combine(root, "utf8.txt"), "caf\u00e9 COMMON_TEXT\n", OldTime);
    WriteFile(Path.Combine(root, "utf8bom.txt"), "caf\u00e9 with BOM COMMON_TEXT\n", OldTime, new UTF8Encoding(true));
    WriteFile(Path.Combine(root, "utf16.txt"), "UTF16_MARKER caf\u00e9 COMMON_TEXT\n", OldTime, Encoding.Unicode);
    File.WriteAllBytes(Path.Combine(root, "image.bin"), new byte[] { 0, 1, 2, 3, 0, 0, 0, 0, 255, 254 });
    File.SetLastWriteTimeUtc(Path.Combine(root, "image.bin"), OldTime);
    var large = new StringBuilder("// LARGE_MARKER\n");
    while (large.Length < 600 * 1024) {
      large.Append("int large_function() { return COMMON_TEXT; }\n");
    }
    WriteFile(Path.Combine(root, "large.txt"), large.ToString(), OldTime);
    return 1 + DirectoryCount * FilesPerDirectory + 5;
  }

  private static async Task<JsonRpcServerClient> StartAndLoadAsync(string projectFile, string? cacheDirectory) {
    var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath, cacheDirectory);
    await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => server.RequestAsync(ServerProtocol.RegisterFile(projectFile)));
    return server;
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
    Assert.IsNotEmpty(expected["caf\u00e9"]);
    Assert.IsNotEmpty(expected["UTF16_MARKER"]);
    Assert.IsNotEmpty(expected["LARGE_MARKER"]);
  }
}
