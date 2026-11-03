// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// The statistics of the file contents arenas ("fileContentsArenas" of "server/statistics" and
/// "index/statistics"): the text of edited files becomes dead bytes of their arena.
/// </summary>
[TestClass]
public class FileContentsArenaTests {
  private const int FileCount = 100;
  private const string Marker = "ARENA_STATISTICS_MARKER";

  [TestMethod]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task EditedFilesAreDeadBytesOfTheirArena() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumArenaTest-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path.Combine(root, "src"));
    try {
      File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
        "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
      for (var i = 0; i < FileCount; i++) {
        var sb = new StringBuilder();
        sb.Append("// File ").Append(i).Append(" with ").Append(Marker).Append('\n');
        for (var line = 0; line < 20; line++) {
          sb.Append("int function_").Append(i).Append('_').Append(line).Append("();\n");
        }
        File.WriteAllText(Path.Combine(root, "src", $"file{i:D3}.cc"), sb.ToString());
      }

      await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
      await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromMinutes(2),
        () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));

      // All the files of a cold load are live.
      var arenas = await ArenaStatisticsAsync(server);
      Assert.AreEqual(1, arenas["arenaCount"]!.GetValue<int>());
      Assert.AreEqual(16L * 1024 * 1024, arenas["arenaBytes"]!.GetValue<long>());
      var liveBytes = arenas["liveBytes"]!.GetValue<long>();
      Assert.IsTrue(liveBytes > FileCount * 400, $"live bytes: {liveBytes}");
      Assert.AreEqual(arenas["usedBytes"]!.GetValue<long>(), liveBytes);

      // The previous text of an edited file is dead.
      var path = Path.Combine(root, "src", "file000.cc");
      var oldSlotSize = (new FileInfo(path).Length + 2 + 7) / 8 * 8;
      File.AppendAllText(path, "// " + Marker + " again\n");
      var stopwatch = Stopwatch.StartNew();
      while (await HitCountAsync(server) != FileCount + 1) {
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(60), "Timeout waiting for the change");
        await Task.Delay(100);
      }
      arenas = await ArenaStatisticsAsync(server);
      Assert.AreEqual(oldSlotSize, arenas["usedBytes"]!.GetValue<long>() - arenas["liveBytes"]!.GetValue<long>());

      // Same statistics in "index/statistics" (status dialogs).
      var indexArenas = (await server.RequestAsync(ServerProtocol.GetIndexStatistics())).Data["fileContentsArenas"]!;
      Assert.AreEqual(arenas["liveBytes"]!.GetValue<long>(), indexArenas["liveBytes"]!.GetValue<long>());
    }
    finally {
      try {
        Directory.Delete(root, recursive: true);
      }
      catch (IOException) {
        // Best effort
      }
    }
  }

  private static async Task<JsonNode> ArenaStatisticsAsync(JsonRpcServerClient server) {
    var response = await server.RequestAsync(ServerProtocol.GetServerStatistics());
    return response.Data["fileContentsArenas"]!;
  }

  private static async Task<long> HitCountAsync(JsonRpcServerClient server) {
    var response = await server.RequestAsync(ServerProtocol.SearchCode(Marker, matchCase: true));
    return response.Data["hitCount"]!.GetValue<long>();
  }
}
