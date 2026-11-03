// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Tests.EndToEnd.Corpus;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Protocol;

/// <summary>
/// Smoke tests of the typed messages protocol ("--stdio", JSON lines), still used by the clients
/// until they move to the JSON-RPC protocol (phases P3/P4 of the protocol migration). Removed with
/// that protocol (P5). All the other tests and the benchmarks use the JSON-RPC protocol.
/// </summary>
[TestClass]
public class LegacyProtocolSmokeTests {
  private const string Marker = "LEGACY_SMOKE_MARKER";

  private static void GenerateFiles(string root) {
    File.WriteAllText(Path.Combine(root, "vs-chromium-project.txt"),
      "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n");
    Directory.CreateDirectory(Path.Combine(root, "src"));
    File.WriteAllText(Path.Combine(root, "src", "file.cc"), "// first line\nint x = 0; // " + Marker + "\n");
    File.WriteAllText(Path.Combine(root, "other.txt"), "no marker here\n");
  }

  [TestMethod]
  [Timeout(2 * 60 * 1000, CooperativeCancellation = true)]
  public async Task HelloRegisterSearchAndExtractsWork() {
    var root = CorpusCache.GetOrCreateCustom("legacy-smoke", version: 1, GenerateFiles);
    // StartAsync waits for the "hello" response.
    await using var server = await StdioServerClient.StartAsync(TestSettings.ServerPath);
    await server.WaitForEventAsync("SearchEngineFilesLoaded", TimeSpan.FromMinutes(1),
      () => server.RequestAsync(LegacyServerProtocol.RegisterFile(Path.Combine(root, "vs-chromium-project.txt"))));

    var search = await server.RequestAsync(LegacyServerProtocol.SearchCode(Marker, matchCase: true));
    Assert.AreEqual("SearchCodeResponse", (string?)search.Data["$type"]);
    Assert.AreEqual(1L, search.Data["hitCount"]!.GetValue<long>());
    var file = LegacyServerProtocol.FlattenResults(search.Data["searchResults"]?.AsObject()).Single();
    Assert.AreEqual(Path.Combine(root, "src", "file.cc"), file.FullPath);
    var position = file.Positions.Single();
    Assert.AreEqual("// first line\nint x = 0; // ".Length, position.Position);

    var extracts = await server.RequestAsync(LegacyServerProtocol.GetFileExtracts(file.FullPath, file.Positions));
    var extract = extracts.Data["fileExtracts"]!.AsArray().Single()!;
    StringAssert.Contains((string?)extract["text"], "int x = 0; // " + Marker);
    Assert.AreEqual(1, extract["lineNumber"]!.GetValue<int>());
  }
}
