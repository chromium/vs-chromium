// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Tests.EndToEnd.Regression;

/// <summary>
/// End-to-end tests for project disk usage ("project/directorySizes", "project/measureDirectory",
/// and "project/addIgnorePattern").
/// </summary>
[TestClass]
public class ProjectDiskUsageTests {
  public TestContext TestContext { get; set; } = null!;

  [TestMethod]
  [Timeout(2 * 60 * 1000, CooperativeCancellation = true)]
  public async Task DirectorySizesMeasureAndAddIgnorePatternUpdateIndexEndToEnd() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumDiskUsageE2E-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try {
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");
      const string initialProjectFile =
        "# Custom project\r\n" +
        "[SourceExplorer.ignore]\r\n" +
        "/excluded_out/\r\n" +
        "\r\n" +
        "[SearchableFiles.include]\r\n" +
        "*\r\n" +
        "\r\n" +
        "[SearchableFiles.ignore]\r\n" +
        "*.png\r\n";
      File.WriteAllText(projectFile, initialProjectFile, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

      Directory.CreateDirectory(Path.Combine(root, "big_logs"));
      var logA = new string('a', 4096) + "\n";
      var logB = new string('b', 2048) + "\n";
      File.WriteAllText(Path.Combine(root, "big_logs", "a.log"), logA);
      File.WriteAllText(Path.Combine(root, "big_logs", "b.log"), logB);

      Directory.CreateDirectory(Path.Combine(root, "assets"));
      var pngBytes = new byte[3000];
      pngBytes[0] = 0x89;
      pngBytes[1] = 0x00;
      File.WriteAllBytes(Path.Combine(root, "assets", "banner.png"), pngBytes);
      var codeText = "int main() { return 42; }\n";
      File.WriteAllText(Path.Combine(root, "assets", "helper.cc"), codeText);

      Directory.CreateDirectory(Path.Combine(root, "excluded_out", "sub"));
      File.WriteAllBytes(Path.Combine(root, "excluded_out", "artifact.bin"), new byte[12_000]);
      File.WriteAllBytes(Path.Combine(root, "excluded_out", "sub", "more.bin"), new byte[8_000]);

      await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
      await server.RequestAsync(ServerProtocol.RegisterFile(projectFile));

      await WaitForIdleWithFilesAsync(server, expectedFiles: 5); // projectFile + 2 logs + png + helper.cc

      // 1. Root directory sizes: big_logs (~6 KB searchable), assets (3000 non-searchable + helper.cc searchable),
      // vs-chromium-project.txt, and excluded_out (excluded, notMeasured).
      var rootSizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, ""))).Data;
      Assert.AreEqual("", rootSizes["relativePath"]!.GetValue<string>());
      Assert.AreEqual(projectFile, rootSizes["projectFile"]!.GetValue<string>());

      var rootChildren = rootSizes["children"]!.AsArray();
      Assert.AreEqual("big_logs", rootChildren[0]!["name"]!.GetValue<string>());
      Assert.IsTrue(rootChildren[0]!["searchableBytes"]!.GetValue<long>() > 6000);
      Assert.AreEqual(0L, rootChildren[0]!["notSearchableBytes"]!.GetValue<long>());

      var assetsRow = rootChildren.Single(c => c!["name"]!.GetValue<string>() == "assets")!;
      Assert.AreEqual(3000L, assetsRow["notSearchableBytes"]!.GetValue<long>());
      Assert.AreEqual((long)Encoding.UTF8.GetByteCount(codeText), assetsRow["searchableBytes"]!.GetValue<long>());

      var excludedRow = rootChildren.Single(c => c!["name"]!.GetValue<string>() == "excluded_out")!;
      Assert.IsTrue(excludedRow["excluded"]!.GetValue<bool>());
      Assert.AreEqual("notMeasured", excludedRow["measureState"]!.GetValue<string>());
      Assert.AreEqual("/excluded_out/", excludedRow["pattern"]!.GetValue<string>());

      // 2. Drill into "assets" and verify per-file breakdown.
      var assetsSizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, "assets"))).Data;
      var assetsChildren = assetsSizes["children"]!.AsArray();
      Assert.AreEqual("banner.png", assetsChildren[0]!["name"]!.GetValue<string>());
      Assert.AreEqual(3000L, assetsChildren[0]!["totalBytes"]!.GetValue<long>());
      Assert.AreEqual(0L, assetsChildren[0]!["searchableBytes"]!.GetValue<long>());
      Assert.AreEqual("/assets/banner.png", assetsChildren[0]!["pattern"]!.GetValue<string>());

      // 3. Measure excluded_out on demand (20,000 bytes across 2 files).
      JsonObject measure = (await server.RequestAsync(ServerProtocol.MeasureDirectory(root, "excluded_out"))).Data;
      var measureDeadline = Stopwatch.StartNew();
      while (measure["state"]!.GetValue<string>() == "running" && measureDeadline.Elapsed < TimeSpan.FromSeconds(10)) {
        await Task.Delay(25);
        measure = (await server.RequestAsync(ServerProtocol.MeasureDirectory(root, "excluded_out"))).Data;
      }
      Assert.AreEqual("done", measure["state"]!.GetValue<string>());
      Assert.AreEqual(20_000L, measure["totalBytes"]!.GetValue<long>());
      Assert.AreEqual(2L, measure["fileCount"]!.GetValue<long>());

      var rootAfterMeasure = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, ""))).Data;
      var firstChildAfterMeasure = rootAfterMeasure["children"]!.AsArray()[0]!;
      Assert.AreEqual("excluded_out", firstChildAfterMeasure["name"]!.GetValue<string>());
      Assert.AreEqual(20_000L, firstChildAfterMeasure["totalBytes"]!.GetValue<long>());

      // 4. Add "/big_logs/" to [SearchableFiles.ignore]: keeps directory in tree (5 files), drops searchableBytes to 0.
      var addSearchableIgnore = (await server.RequestAsync(
        ServerProtocol.AddIgnorePattern(root, "searchableFilesIgnore", "/big_logs/"))).Data;
      Assert.IsTrue(addSearchableIgnore["added"]!.GetValue<bool>());
      Assert.AreEqual("SearchableFiles.ignore", addSearchableIgnore["sectionName"]!.GetValue<string>());

      await WaitForConditionAsync(async () => {
        var sizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, ""))).Data;
        var row = sizes["children"]!.AsArray().FirstOrDefault(c => c!["name"]!.GetValue<string>() == "big_logs");
        return row != null && row["searchableBytes"]!.GetValue<long>() == 0;
      }, "big_logs excluded from searchable files");

      // Duplicate add is a no-op.
      var duplicateAdd = (await server.RequestAsync(
        ServerProtocol.AddIgnorePattern(root, "searchableFilesIgnore", "/big_logs/"))).Data;
      Assert.IsFalse(duplicateAdd["added"]!.GetValue<bool>());

      // 5. Add "/assets/" to [SourceExplorer.ignore]: removes assets (2 files) from index and marks it excluded.
      var addExplorerIgnore = (await server.RequestAsync(
        ServerProtocol.AddIgnorePattern(root, "sourceExplorerIgnore", "/assets/"))).Data;
      Assert.IsTrue(addExplorerIgnore["added"]!.GetValue<bool>());

      await WaitForIdleWithFilesAsync(server, expectedFiles: 3); // projectFile + 2 logs (assets excluded!)
      var finalSizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, ""))).Data;
      var finalAssets = finalSizes["children"]!.AsArray().Single(c => c!["name"]!.GetValue<string>() == "assets")!;
      Assert.IsTrue(finalAssets["excluded"]!.GetValue<bool>());

      // Comments and CRLF in vs-chromium-project.txt were preserved.
      var updatedFileText = File.ReadAllText(projectFile);
      StringAssert.StartsWith(updatedFileText, "# Custom project\r\n");
    }
    finally {
      try {
        Directory.Delete(root, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      }
    }
  }

  /// <summary>
  /// Symbolic links (inside, outside, dangling): listed with their target, not counted in the
  /// sizes. Inconclusive if symbolic links can't be created.
  /// </summary>
  [TestMethod]
  [Timeout(2 * 60 * 1000, CooperativeCancellation = true)]
  public async Task DirectorySizesOfSymbolicLinks() {
    var parent = Path.Combine(Path.GetTempPath(), "VsChromiumDiskUsageLinksE2E-" + Guid.NewGuid().ToString("N"));
    var root = Path.Combine(parent, "project");
    var outside = Path.Combine(parent, "outside");
    Directory.CreateDirectory(Path.Combine(root, "src"));
    Directory.CreateDirectory(Path.Combine(outside, "dir"));
    try {
      var projectFile = Path.Combine(root, "vs-chromium-project.txt");
      File.WriteAllText(projectFile, "[SourceExplorer.ignore]\n\n[SearchableFiles.include]\n*\n\n[SearchableFiles.ignore]\n",
        new UTF8Encoding(false));
      File.WriteAllText(Path.Combine(root, "src", "real.cc"), new string('r', 100));
      File.WriteAllText(Path.Combine(outside, "out.cc"), new string('o', 50));
      File.WriteAllText(Path.Combine(outside, "dir", "d.cc"), new string('d', 70));
      try {
        File.CreateSymbolicLink(Path.Combine(root, "src", "link.cc"), Path.Combine(root, "src", "real.cc"));
        Directory.CreateSymbolicLink(Path.Combine(root, "dirlink"), Path.Combine(root, "src"));
        File.CreateSymbolicLink(Path.Combine(root, "outfile.cc"), Path.Combine(outside, "out.cc"));
        Directory.CreateSymbolicLink(Path.Combine(root, "outdir"), Path.Combine(outside, "dir"));
        File.CreateSymbolicLink(Path.Combine(root, "dangling.cc"), Path.Combine(root, "missing.cc"));
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        Assert.Inconclusive("Symbolic links can't be created: " + e.Message);
      }

      await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
      await server.RequestAsync(ServerProtocol.RegisterFile(projectFile));
      await WaitForConditionAsync(async () => {
        var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
        return status["projectCount"]!.GetValue<int>() == 1 && status["status"]!.GetValue<string>() == "idle" &&
               status["fileCount"]!.GetValue<int>() >= 5;
      }, "project loaded");

      var sizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, ""))).Data;
      TestContext.WriteLine(sizes.ToJsonString());
      var projectFileLength = new FileInfo(projectFile).Length;
      Assert.AreEqual(100 + projectFileLength, sizes["totals"]!["totalBytes"]!.GetValue<long>(), "Only the real files");
      Assert.AreEqual(2L, sizes["totals"]!["fileCount"]!.GetValue<long>());
      var children = sizes["children"]!.AsArray().ToDictionary(x => x!["name"]!.GetValue<string>(), x => x!);
      Assert.AreEqual(100L, children["src"]["totalBytes"]!.GetValue<long>(), "src/link.cc not counted");

      JsonNode Link(string name) {
        var entry = children[name];
        Assert.AreEqual(0L, entry["totalBytes"]!.GetValue<long>(), name);
        Assert.AreEqual(0L, entry["fileCount"]!.GetValue<long>(), name);
        Assert.IsNotNull(entry["symbolicLink"], name);
        return entry["symbolicLink"]!;
      }
      var dirLink = Link("dirlink");
      Assert.AreEqual("directory", dirLink["kind"]!.GetValue<string>());
      Assert.AreEqual(root, dirLink["targetProjectPath"]!.GetValue<string>());
      Assert.AreEqual("src", dirLink["targetRelativePath"]!.GetValue<string>());
      Assert.AreEqual(100L, dirLink["targetTotalBytes"]!.GetValue<long>(), dirLink.ToJsonString());
      Assert.AreEqual("/dirlink/", children["dirlink"]["pattern"]!.GetValue<string>());

      var outFile = Link("outfile.cc");
      Assert.AreEqual("file", outFile["kind"]!.GetValue<string>());
      Assert.IsTrue(outFile["external"]!.GetValue<bool>());
      Assert.AreEqual(Path.Combine(outside, "out.cc"), outFile["target"]!.GetValue<string>(), ignoreCase: true);
      Assert.AreEqual(50L, outFile["targetTotalBytes"]!.GetValue<long>());
      Assert.IsNull(outFile["targetProjectPath"]);

      var outDir = Link("outdir");
      Assert.IsTrue(outDir["external"]!.GetValue<bool>());
      Assert.AreEqual(70L, outDir["targetTotalBytes"]!.GetValue<long>());
      Assert.AreEqual(1L, outDir["targetFileCount"]!.GetValue<long>());

      {
        // Note: The scan keeps the dangling links (a file of length 0).
        var dangling = Link("dangling.cc");
        Assert.IsTrue(dangling["dangling"]!.GetValue<bool>(), dangling.ToJsonString());
        Assert.AreEqual(Path.Combine(root, "missing.cc"), dangling["target"]!.GetValue<string>(), ignoreCase: true);
      }

      var srcSizes = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(root, "src"))).Data;
      var fileLink = srcSizes["children"]!.AsArray().Single(x => x!["name"]!.GetValue<string>() == "link.cc")!;
      Assert.AreEqual(0L, fileLink["totalBytes"]!.GetValue<long>());
      Assert.AreEqual("src/real.cc", fileLink["symbolicLink"]!["targetRelativePath"]!.GetValue<string>());
      Assert.AreEqual(100L, fileLink["symbolicLink"]!["targetTotalBytes"]!.GetValue<long>());
    }
    finally {
      try {
        Directory.Delete(parent, recursive: true);
      }
      catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
      }
    }
  }

  [TestMethod]
  [TestCategory("Benchmark")]
  [Timeout(5 * 60 * 1000, CooperativeCancellation = true)]
  public async Task LargeCorpusDirectorySizesAggregationLatency() {
    const string corpusRoot = @"D:\vschromium-corpora\large-1-400000-v1";
    var projectFile = Path.Combine(corpusRoot, "vs-chromium-project.txt");
    if (!File.Exists(projectFile)) {
      Assert.Inconclusive($"Corpus not present at {corpusRoot}");
    }

    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    await server.RequestAsync(ServerProtocol.RegisterFile(projectFile));
    await WaitForConditionAsync(async () => {
      var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
      return status["projectCount"]!.GetValue<int>() == 1 &&
             status["status"]!.GetValue<string>() == "idle" &&
             status["fileCount"]!.GetValue<int>() > 100_000;
    }, "large corpus loaded", timeoutSeconds: 240);

    var first = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(corpusRoot, ""))).Data;
    var computeMs = first["computeMs"]!.GetValue<double>();
    var second = (await server.RequestAsync(ServerProtocol.GetDirectorySizes(corpusRoot, ""))).Data;
    var cachedMs = second["computeMs"]!.GetValue<double>();
    TestContext.WriteLine(
      $"400k corpus directorySizes: first computeMs={computeMs:F2} ms, cached computeMs={cachedMs:F2} ms, " +
      $"totalBytes={first["totals"]!["totalBytes"]}, searchableBytes={first["totals"]!["searchableBytes"]}, " +
      $"files={first["totals"]!["fileCount"]}");
    Assert.IsTrue(computeMs < 500, $"Expected fast single-pass aggregation (<500 ms), got {computeMs:F2} ms");
    Assert.AreEqual(0.0, cachedMs);
  }

  private static async Task WaitForIdleWithFilesAsync(JsonRpcServerClient server, int expectedFiles) {
    await WaitForConditionAsync(async () => {
      var status = (await server.RequestAsync(ServerProtocol.GetIndexStatus())).Data;
      return status["projectCount"]!.GetValue<int>() == 1 &&
             status["status"]!.GetValue<string>() == "idle" &&
             status["fileCount"]!.GetValue<int>() == expectedFiles;
    }, $"idle with {expectedFiles} files");
  }

  private static async Task WaitForConditionAsync(
    Func<Task<bool>> predicate,
    string description,
    int timeoutSeconds = 30) {
    var sw = Stopwatch.StartNew();
    while (sw.Elapsed < TimeSpan.FromSeconds(timeoutSeconds)) {
      if (await predicate()) {
        return;
      }
      await Task.Delay(50);
    }
    Assert.Fail($"Timed out waiting for: {description}");
  }
}
