// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// The verification of the index cache from the change journal (Windows, NTFS/ReFS): after a
/// restart, only the directories with records in the journal since the save of the cache are
/// listed again (catch-up), and the index must be the same as a cold load. The project is in the
/// temporary directory: the tests are inconclusive if its volume has no active change journal.
/// The fallbacks to the full scan are tested too: invalid checkpoint (as if the journal wrapped),
/// and a volume without journal (the Dev Drive by default, <see cref="NoJournalDirectoryVariable"/>).
/// </summary>
[TestClass]
public class ChangeJournalTests {
  public TestContext TestContext { get; set; } = null!;

  private const int NoLimit = 10_000_000;
  private const int DirectoryCount = 20;
  private const int FilesPerDirectory = 20;
  private const string VerifyDelayVariable = "VSCHROMIUM_TEST_VERIFY_DELAY_MS";

  /// <summary>
  /// A directory on a volume whose change journal is not active (e.g. a Dev Drive), for the test of
  /// the fallback (inconclusive if not set).
  /// </summary>
  public const string NoJournalDirectoryVariable = "VSCHROMIUM_TEST_NO_JOURNAL_DIR";

  private static readonly DateTime OldTime = DateTime.UtcNow.AddHours(-1);

  private static readonly string[] Queries = { "JOURNAL_MARKER_", "CHANGED_", "MOVED_IN_", "SYMLINK_TARGET_" };

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RestartWithoutChangesListsNoDirectory() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    var coldResults = await ColdLoadAndWriteCacheAsync(tree);

    await using var server = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(server.Client);
    var verification = await LastVerificationAsync(server.Client, tree);
    AssertJournalVerification(verification);
    Assert.AreEqual(0, verification["listedDirectoryCount"]!.GetValue<int>(), verification.ToJsonString());
    AssertSameResults(coldResults, await AllResultsAsync(server.Client));
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ChangesWhileTheServerIsStoppedAreCaughtUpFromTheJournal() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    var outside = Path.Combine(tree.Parent, "outside");
    Directory.CreateDirectory(Path.Combine(outside, "moved_in", "inner"));
    TestTree.WriteFile(Path.Combine(outside, "moved_in", "inner", "file.cc"), "// MOVED_IN_ subtree\n", OldTime);
    var target = Path.Combine(tree.Root, "tools", "target.cc");
    Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    TestTree.WriteFile(target, "// SYMLINK_TARGET_ original\n", OldTime);
    var hasSymLink = TryCreateFileSymbolicLink(Path.Combine(tree.Root, "dir000", "link.cc"), Path.Combine("..", "tools", "target.cc"));
    await ColdLoadAndWriteCacheAsync(tree);

    // Changes while no server runs.
    var sameSize = tree.FilePath(1, 1);
    TestTree.WriteFile(sameSize, File.ReadAllText(sameSize).Replace("JOURNAL_MARKER_", "CHANGED_SAME__"), OldTime.AddMinutes(1));
    TestTree.WriteFile(Path.Combine(tree.Root, "dir002", "new.cc"), "// CHANGED_NEW_FILE\n", OldTime);
    File.Delete(tree.FilePath(3, 3));
    Directory.CreateDirectory(Path.Combine(tree.Root, "newdir", "sub"));
    TestTree.WriteFile(Path.Combine(tree.Root, "newdir", "sub", "new.cc"), "// CHANGED_NEW_DIR\n", OldTime);
    Directory.Delete(Path.Combine(tree.Root, "dir004"), recursive: true);
    Directory.Move(Path.Combine(tree.Root, "dir005"), Path.Combine(tree.Root, "dir005_renamed"));
    Directory.Move(Path.Combine(outside, "moved_in"), Path.Combine(tree.Root, "dir006", "moved_in"));
    Directory.Move(Path.Combine(tree.Root, "dir007"), Path.Combine(outside, "dir007"));
    if (hasSymLink) {
      TestTree.WriteFile(target, "// SYMLINK_TARGET_ changed, longer\n", OldTime.AddMinutes(2));
    }

    await using var server = await StartAndLoadAsync(tree, verifyDelayMs: 1000);
    Assert.IsTrue(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await WaitForIdleAsync(server.Client);
    var verification = await LastVerificationAsync(server.Client, tree);
    AssertJournalVerification(verification);
    // The root (new directory, deleted, renamed and moved directories), dir001, dir002, dir003,
    // dir006: a few directories, not the 20+ of the project.
    var listed = verification["listedDirectoryCount"]!.GetValue<int>();
    Assert.IsTrue(listed is > 0 and <= 8, verification.ToJsonString());
    Assert.IsTrue(verification["scannedDirectoryCount"]!.GetValue<int>() >= 3, "newdir, dir005_renamed, moved_in: " + verification.ToJsonString());
    if (hasSymLink) {
      Assert.AreEqual(1, verification["symLinkFileCount"]!.GetValue<int>(), verification.ToJsonString());
    }
    var verified = await AllResultsAsync(server.Client);
    Assert.AreEqual(3, verified["CHANGED_"].Count, string.Join(", ", verified["CHANGED_"]));
    Assert.AreEqual(1, verified["MOVED_IN_"].Count);
    if (hasSymLink) {
      Assert.IsTrue(verified["SYMLINK_TARGET_"].Count == 2 && (await HitCountAsync(server.Client, "changed, longer")) == 2,
        string.Join(", ", verified["SYMLINK_TARGET_"]));
    }

    // Same index as a cold load.
    await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
    await WaitForIdleAsync(coldServer.Client);
    AssertSameResults(await AllResultsAsync(coldServer.Client), verified);
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task ChangesWhileTheServerRunsAdvanceTheCheckpointOfTheCache() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);

    // A change while the server runs: the save after it checks the journal (self-check) and saves
    // a checkpoint after the change.
    await using (var server = await StartAndLoadAsync(tree, saveDelays: "500,2000")) {
      await WaitForIdleAsync(server.Client);
      var writes = await CacheWriteCountAsync(server.Client);
      TestTree.WriteFile(Path.Combine(tree.Root, "dir001", "new.cc"), "// CHANGED_WHILE_RUNNING\n", OldTime);
      await WaitForAsync(async () => await HitCountAsync(server.Client, "CHANGED_WHILE_RUNNING") == 1, "the change is indexed");
      // The save of the index with the change (started after the update, completed).
      await WaitForAsync(async () => {
        var cache = (await server.Client.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["indexCache"]!;
        return !cache["dirty"]!.GetValue<bool>() && !cache["saving"]!.GetValue<bool>() && await CacheWriteCountAsync(server.Client) > writes;
      }, "the cache is saved");
    }

    await using var restarted = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(restarted.Client);
    var verification = await LastVerificationAsync(restarted.Client, tree);
    AssertJournalVerification(verification);
    Assert.AreEqual(0, verification["listedDirectoryCount"]!.GetValue<int>(), "The change is before the checkpoint: " + verification.ToJsonString());
    Assert.AreEqual(1, await HitCountAsync(restarted.Client, "CHANGED_WHILE_RUNNING"));
  }

  /// <summary>
  /// The idle advance of the checkpoint: while the index doesn't change, the checkpoint of the
  /// cache is saved alone (journal.bin, the index is not written) after a journal self-check.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task IdleServerAdvancesTheCheckpointOfTheCache() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    await using (var server = await StartAndLoadAsync(tree, checkpointIntervalMs: 500)) {
      await WaitForIdleAsync(server.Client);
      await WaitForAsync(async () => await CacheWriteCountAsync(server.Client) >= 1, "the cache is written");
      var index = Directory.GetFiles(tree.CacheDirectory, "index.bin", SearchOption.AllDirectories).Single();
      var indexTime = File.GetLastWriteTimeUtc(index);
      var writes = await CacheWriteCountAsync(server.Client);
      // Changes outside of the project (the journal moves on).
      for (var i = 0; i < 3; i++) {
        File.WriteAllText(Path.Combine(tree.Parent, $"outside{i}.txt"), "outside");
        await Task.Delay(300);
      }
      await WaitForAsync(async () => {
        var cache = (await server.Client.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
        return (cache["checkpointUpdateCount"]?.GetValue<int>() ?? 0) >= 1;
      }, "the checkpoint is saved");
      Assert.IsTrue(File.Exists(Path.Combine(Path.GetDirectoryName(index)!, "journal.bin")));
      Assert.AreEqual(indexTime, File.GetLastWriteTimeUtc(index), "The index is not written");
      Assert.AreEqual(writes, await CacheWriteCountAsync(server.Client));
    }

    await using var restarted = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(restarted.Client);
    var verification = await LastVerificationAsync(restarted.Client, tree);
    AssertJournalVerification(verification);
    Assert.AreEqual(0, verification["listedDirectoryCount"]!.GetValue<int>(), verification.ToJsonString());
  }

  /// <summary>
  /// Directory symbolic links to the same volume: the changes in their target are caught up from
  /// the journal (the subtree is not scanned), a retargeted link and a link whose target was
  /// renamed outside of the project are scanned entirely.
  /// </summary>
  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task DirectorySymbolicLinksToTheSameVolume() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    var outside = Path.Combine(tree.Parent, "outside");
    foreach (var name in new[] { "changed", "retarget1", "retarget_two", Path.Combine("parent", "target"), Path.Combine("other", "target") }) {
      Directory.CreateDirectory(Path.Combine(outside, name, "inner"));
      TestTree.WriteFile(Path.Combine(outside, name, "inner", "file.cc"), $"// SYMLINK_TARGET_ {name.Replace('\\', '/')}\n", OldTime);
    }
    try {
      Directory.CreateSymbolicLink(Path.Combine(tree.Root, "link_changed"), Path.Combine(outside, "changed"));
      Directory.CreateSymbolicLink(Path.Combine(tree.Root, "link_retarget"), Path.Combine(outside, "retarget1"));
      // Note: In another directory than the retargeted link (whose directory is listed again: its
      // symbolic links are scanned entirely).
      Directory.CreateSymbolicLink(Path.Combine(tree.Root, "dir010", "link_renamed"), Path.Combine(outside, "parent", "target"));
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
    }
    await ColdLoadAndWriteCacheAsync(tree);

    // 1. A change in the target of a link, while no server runs: from the journal.
    TestTree.WriteFile(Path.Combine(outside, "changed", "inner", "new.cc"), "// CHANGED_IN_TARGET\n", OldTime);
    await using (var server = await StartAndLoadAsync(tree)) {
      await WaitForIdleAsync(server.Client);
      var verification = await LastVerificationAsync(server.Client, tree);
      AssertJournalVerification(verification);
      Assert.AreEqual(0, verification["symLinkDirectoryCount"]!.GetValue<int>(), "No subtree scanned: " + verification.ToJsonString());
      Assert.AreEqual(1, await HitCountAsync(server.Client, "CHANGED_IN_TARGET"));
    }

    // 2. A link retargeted, and 3. the parent of the target of a link renamed outside of the
    // project (the link resolves to another directory): scanned entirely.
    Directory.Delete(Path.Combine(tree.Root, "link_retarget"));
    Directory.CreateSymbolicLink(Path.Combine(tree.Root, "link_retarget"), Path.Combine(outside, "retarget_two"));
    Directory.Move(Path.Combine(outside, "parent"), Path.Combine(outside, "parent_renamed"));
    Directory.Move(Path.Combine(outside, "other"), Path.Combine(outside, "parent"));
    await using (var server = await StartAndLoadAsync(tree)) {
      await WaitForIdleAsync(server.Client);
      var verification = await LastVerificationAsync(server.Client, tree);
      AssertJournalVerification(verification);
      // link_renamed: found by the check of the targets; link_retarget: a symbolic link of a
      // directory listed again (the root), scanned as a new directory.
      Assert.AreEqual(1, verification["symLinkDirectoryCount"]!.GetValue<int>(), verification.ToJsonString());
      Assert.IsTrue(verification["scannedDirectoryCount"]!.GetValue<int>() >= 1, verification.ToJsonString());
      Assert.AreEqual(1, await HitCountAsync(server.Client, "SYMLINK_TARGET_ retarget_two"));
      Assert.AreEqual(0, await HitCountAsync(server.Client, "SYMLINK_TARGET_ retarget1"));
      Assert.AreEqual(1, await HitCountAsync(server.Client, "SYMLINK_TARGET_ other/target"));
      Assert.AreEqual(0, await HitCountAsync(server.Client, "SYMLINK_TARGET_ parent/target"));
      var verified = await AllResultsAsync(server.Client);

      // Same index as a cold load.
      await using var coldServer = await StartAndLoadAsync(tree, cacheDirectory: null);
      await WaitForIdleAsync(coldServer.Client);
      AssertSameResults(await AllResultsAsync(coldServer.Client), verified);
    }
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task InvalidCheckpointFallsBackToAFullScan() {
    using var tree = new TestTree(TestTree.TempRoot());
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    TestTree.WriteFile(Path.Combine(tree.Root, "dir002", "new.cc"), "// CHANGED_NEW_FILE\n", OldTime);
    // As if the journal wrapped: a checkpoint before the first record of the journal.
    SetCheckpointUsn(tree, -8);

    await using var server = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(server.Client);
    var verification = await LastVerificationAsync(server.Client, tree);
    if (verification["fallbackReason"]?.GetValue<string>() is string reason && reason.Contains("not active")) {
      Assert.Inconclusive("No change journal: " + reason);
    }
    Assert.AreEqual("fullScan", verification["method"]!.GetValue<string>(), verification.ToJsonString());
    StringAssert.Contains(verification["fallbackReason"]!.GetValue<string>(), "wrapped");
    StringAssert.Contains(verification["hint"]!.GetValue<string>(), "fsutil usn createjournal");
    Assert.AreEqual(-8L, verification["checkpointUsn"]!.GetValue<long>());
    Assert.IsNotNull(verification["checkpointTimeUtc"]);
    Assert.AreEqual(1, await HitCountAsync(server.Client, "CHANGED_NEW_FILE"));
  }

  [TestMethod]
  [Timeout(10 * 60 * 1000, CooperativeCancellation = true)]
  public async Task VolumeWithoutJournalFallsBackToAFullScan() {
    var directory = Environment.GetEnvironmentVariable(NoJournalDirectoryVariable);
    if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
      Assert.Inconclusive($"Set {NoJournalDirectoryVariable} to a directory of a volume without change journal (e.g. a Dev Drive).");
    using var tree = new TestTree(Path.Combine(directory, "VsChromiumChangeJournalTest-" + Guid.NewGuid().ToString("N")));
    tree.Generate();
    await ColdLoadAndWriteCacheAsync(tree);
    TestTree.WriteFile(Path.Combine(tree.Root, "dir002", "new.cc"), "// CHANGED_NEW_FILE\n", OldTime);

    await using var server = await StartAndLoadAsync(tree);
    await WaitForIdleAsync(server.Client);
    var verification = await LastVerificationAsync(server.Client, tree);
    Assert.AreEqual("fullScan", verification["method"]!.GetValue<string>(), verification.ToJsonString());
    TestContext.WriteLine("Fallback: " + verification["fallbackReason"]);
    Assert.IsFalse(string.IsNullOrEmpty(verification["fallbackReason"]?.GetValue<string>()));
    Assert.AreEqual(1, await HitCountAsync(server.Client, "CHANGED_NEW_FILE"));
  }

  private static void AssertJournalVerification(JsonNode verification) {
    if (verification["method"]!.GetValue<string>() != "changeJournal") {
      Assert.Inconclusive("The change journal is not used on the temporary directory: " + verification.ToJsonString());
    }
  }

  private static bool TryCreateFileSymbolicLink(string path, string target) {
    try {
      File.CreateSymbolicLink(path, target);
      return true;
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      // Symbolic links can't be created (no developer mode): tested without.
      return false;
    }
  }

  /// <summary>Overwrites the USN of the change journal checkpoint in the header of the index of the cache.</summary>
  private static void SetCheckpointUsn(TestTree tree, long usn) {
    var index = Directory.GetFiles(tree.CacheDirectory, "index.bin", SearchOption.AllDirectories).Single();
    using var stream = new FileStream(index, FileMode.Open, FileAccess.ReadWrite);
    using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
    reader.ReadUInt32(); // Magic
    reader.ReadInt32(); // Format version
    reader.ReadBoolean(); // Case sensitive
    reader.ReadString(); // Root path
    reader.ReadString(); // Version hash
    Assert.IsTrue(reader.ReadBoolean(), "The cache has a checkpoint");
    reader.ReadUInt64(); // Volume serial number
    reader.ReadUInt64(); // Journal id
    // Note: Followed by the time of the checkpoint (kept).
    using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
    writer.Write(usn);
  }

  private static async Task<JsonNode> LastVerificationAsync(JsonRpcServerClient server, TestTree tree) {
    var details = (await server.RequestAsync(ServerProtocol.GetProjectDetails(tree.Root))).Data;
    var verification = details["operations"]!["lastVerification"];
    Assert.IsNotNull(verification, details["operations"]!.ToJsonString());
    return verification;
  }

  /// <summary>A generated project and its cache directory (deleted when disposed).</summary>
  private sealed class TestTree : IDisposable {
    public TestTree(string parent) {
      Parent = parent;
      Root = Path.Combine(parent, "project");
      CacheDirectory = Path.Combine(parent, "cache");
      Directory.CreateDirectory(Root);
      Directory.CreateDirectory(CacheDirectory);
    }

    public static string TempRoot() => Path.Combine(Path.GetTempPath(), "VsChromiumChangeJournalTest-" + Guid.NewGuid().ToString("N"));

    public string Parent { get; }
    public string Root { get; }
    public string CacheDirectory { get; }
    public string ProjectFile => Path.Combine(Root, "vs-chromium-project.txt");

    public string FilePath(int directory, int file) => Path.Combine(Root, $"dir{directory:D3}", $"file{file:D3}.cc");

    public static void WriteFile(string path, string text, DateTime lastWriteTimeUtc) {
      File.WriteAllText(path, text, new UTF8Encoding(false));
      File.SetLastWriteTimeUtc(path, lastWriteTimeUtc);
    }

    public void Generate() {
      WriteFile(ProjectFile, "[SourceExplorer.ignore]\nignored/\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n", OldTime);
      for (var d = 0; d < DirectoryCount; d++) {
        Directory.CreateDirectory(Path.Combine(Root, $"dir{d:D3}", "nested"));
        for (var f = 0; f < FilesPerDirectory; f++) {
          WriteFile(FilePath(d, f), $"// File {d}/{f} JOURNAL_MARKER_{d}_{f}\nint function_{f}() {{ return {d}; }}\n", OldTime);
        }
        WriteFile(Path.Combine(Root, $"dir{d:D3}", "nested", "nested.cc"), $"// JOURNAL_MARKER_nested_{d}\n", OldTime);
      }
      Directory.CreateDirectory(Path.Combine(Root, "ignored"));
      WriteFile(Path.Combine(Root, "ignored", "ignored.cc"), "// ignored\n", OldTime);
    }

    public void Dispose() {
      try {
        Directory.Delete(Parent, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        // Best effort
      }
    }
  }

  /// <summary>A started server, and the first "index/loaded" notification.</summary>
  private sealed class LoadedServer(JsonRpcServerClient client, JsonObject firstLoaded) : IAsyncDisposable {
    public JsonRpcServerClient Client { get; } = client;
    public JsonObject FirstLoaded { get; } = firstLoaded;
    public ValueTask DisposeAsync() => Client.DisposeAsync();
  }

  private static Task<LoadedServer> StartAndLoadAsync(TestTree tree, int verifyDelayMs = 0, string? saveDelays = null,
                                                      int checkpointIntervalMs = 0) {
    return StartAndLoadAsync(tree, tree.CacheDirectory, verifyDelayMs, saveDelays, checkpointIntervalMs);
  }

  private static async Task<LoadedServer> StartAndLoadAsync(TestTree tree, string? cacheDirectory, int verifyDelayMs = 0,
                                                            string? saveDelays = null, int checkpointIntervalMs = 0) {
    var environment = new Dictionary<string, string?> {
      [VerifyDelayVariable] = verifyDelayMs > 0 ? verifyDelayMs.ToString() : null,
      ["VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS"] = saveDelays,
      ["VSCHROMIUM_TEST_CHECKPOINT_INTERVAL_MS"] = checkpointIntervalMs > 0 ? checkpointIntervalMs.ToString() : null,
    };
    var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath, cacheDirectory, environment);
    var loaded = await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
      () => server.RequestAsync(ServerProtocol.RegisterFile(tree.ProjectFile)));
    return new LoadedServer(server, loaded);
  }

  private static async Task<Dictionary<string, List<string>>> ColdLoadAndWriteCacheAsync(TestTree tree) {
    await using var server = await StartAndLoadAsync(tree);
    Assert.IsFalse(server.FirstLoaded["fromCache"]?.GetValue<bool>() ?? false);
    await WaitForIdleAsync(server.Client);
    await WaitForAsync(async () => await CacheWriteCountAsync(server.Client) >= 1, "the cache is written");
    return await AllResultsAsync(server.Client);
  }

  private static async Task<int> CacheWriteCountAsync(JsonRpcServerClient server) {
    var cache = (await server.RequestAsync(ServerProtocol.GetServerStatistics())).Data["indexCache"]!;
    return cache["writeCount"]!.GetValue<int>();
  }

  private static async Task WaitForAsync(Func<Task<bool>> condition, string description) {
    var stopwatch = Stopwatch.StartNew();
    while (!await condition()) {
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), $"Timeout waiting for: {description}");
      await Task.Delay(100);
    }
  }

  private static Task WaitForIdleAsync(JsonRpcServerClient server) {
    return WaitForAsync(async () => (string)(await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["status"]! == "idle",
      "the status \"idle\"");
  }

  private static async Task<long> HitCountAsync(JsonRpcServerClient server, string text) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(text, maxResults: NoLimit, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }

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
    Assert.IsNotEmpty(expected["JOURNAL_MARKER_"]);
  }
}
