// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;
using VsChromium.Bench.Corpora;
using VsChromium.Bench.Inventory;
using VsChromium.Bench.Results;
using VsChromium.Bench.Servers;
using VsChromium.Bench.Shared.Corpus;

namespace VsChromium.Bench.Tests;

[TestClass]
public class EstimateTests {
  [TestMethod]
  public void EstimatesMatchTheMeasured400kCorpus() {
    // Measured: 3,912,356,729 bytes.
    Assert.AreEqual(3_912_356_729, CorpusCatalog.EstimateCorpusBytes(400_000), 0.05 * 3_912_356_729);
    Assert.IsGreaterThan(CorpusCatalog.EstimateCorpusBytes(400_000), CorpusCatalog.EstimateCorpusDiskBytes(400_000));
  }

  [TestMethod]
  public void EstimatesGrowWithTheCorpus() {
    var e400k = CorpusCatalog.Estimate(CorpusCatalog.Spec("400k", false), corpusExists: false);
    var e1m = CorpusCatalog.Estimate(CorpusCatalog.Spec("1M", false), corpusExists: false);
    var e2m = CorpusCatalog.Estimate(CorpusCatalog.Spec("2M", false), corpusExists: false);
    Assert.IsLessThan(e1m.NeededBytes, e400k.NeededBytes);
    Assert.IsLessThan(e2m.NeededBytes, e1m.NeededBytes);
    // The design: about 35 GB in total for 2M files (files and index cache).
    Assert.AreEqual(35.0, e2m.NeededBytes / (double)(1L << 30), 5.0);
  }

  [TestMethod]
  public void ExistingCorpusNeedsTheCacheOnlyAndTheMarginApplies() {
    var spec = CorpusCatalog.Spec("400k", false);
    var fresh = CorpusCatalog.Estimate(spec, corpusExists: false);
    var existing = CorpusCatalog.Estimate(spec, corpusExists: true);
    Assert.AreEqual(fresh.CacheBytes, existing.NeededBytes);
    Assert.AreEqual((long)(fresh.NeededBytes * 1.1), fresh.RequiredFreeBytes);
    Assert.IsTrue(fresh.Fits(fresh.RequiredFreeBytes));
    Assert.IsFalse(fresh.Fits(fresh.NeededBytes)); // Below the +10% margin.
    Assert.IsTrue(existing.Fits(fresh.NeededBytes));
  }

  [TestMethod]
  public void CorpusNamesMapToSpecs() {
    Assert.AreEqual(CorpusSpec.Large, CorpusCatalog.Spec("400k", false));
    Assert.AreEqual(1_000_000, CorpusCatalog.Spec("1M", false).FileCount);
    Assert.AreEqual(2_000_000, CorpusCatalog.Spec("2m", false).FileCount);
    Assert.IsTrue(CorpusCatalog.Spec("400k", true).Symlinks);
    Assert.AreEqual("large-1-400000-symlinks-v2", CorpusCatalog.Spec("400k", true).DirectoryName);
  }
}

[TestClass]
public class ReleaseAssetTests {
  private static ReleaseAsset Asset(string name, long size = 100) => new(name, "https://example.invalid/" + name, size);

  [TestMethod]
  public void PrefersTheServerArchiveOfThePlatform() {
    var assets = new[] {
      Asset("vs-chromium-0.13.0-win-x64.vsix"), Asset("vschromium-server-0.13.0-linux-x64.zip"),
      Asset("vschromium-server-0.13.0-win-x64.zip"), Asset("vschromium-server-0.13.0-win-arm64.zip"), Asset("SHA256SUMS"),
    };
    var selection = ReleaseAssets.Select(assets, "win-x64");
    Assert.IsNotNull(selection);
    Assert.AreEqual("vschromium-server-0.13.0-win-x64.zip", selection.Package.Name);
    Assert.AreEqual("SHA256SUMS", selection.Checksums?.Name);
    Assert.IsFalse(selection.IsVsix);
  }

  [TestMethod]
  public void FallsBackToTheVsixOfThePlatform() {
    var assets = new[] { Asset("vs-chromium-0.13.0-linux-arm64.vsix"), Asset("vs-chromium-0.13.0-linux-x64.vsix"), Asset("vs-chromium-0.13.0-linux-x64.vsix.sha256") };
    var selection = ReleaseAssets.Select(assets, "linux-x64");
    Assert.AreEqual("vs-chromium-0.13.0-linux-x64.vsix", selection?.Package.Name);
    Assert.IsTrue(selection!.IsVsix);
    Assert.AreEqual("vs-chromium-0.13.0-linux-x64.vsix.sha256", selection.Checksums?.Name);
  }

  [TestMethod]
  public void NoMatchingAssetIsReported() {
    // Releases of today: a single platform-independent VS extension.
    var release = new Release("v0.9.38", "0.9.38", false, [Asset("VsChromium.vsix"), Asset("vs-chromium-0.13.0-osx-arm64.vsix")]);
    Assert.IsNull(ReleaseAssets.Select(release.Assets, "win-x64"));
    Assert.IsNull(ReleaseAssets.Select([Asset("vschromium-server-win-x64.zip")], "win-x64")); // No version.
    var message = ReleaseAssets.DescribeMissing(release, "win-x64");
    StringAssert.Contains(message, "v0.9.38");
    StringAssert.Contains(message, "VsChromium.vsix");
    StringAssert.Contains(message, "vschromium-server-<version>-win-x64.zip");
  }

  [TestMethod]
  public void FindsChecksums() {
    var hash = new string('a', 64);
    var other = new string('b', 64);
    var sums = $"{other}  vschromium-server-0.13.0-linux-x64.zip\n{hash.ToUpperInvariant()} *vschromium-server-0.13.0-win-x64.zip\n";
    Assert.AreEqual(hash, ReleaseAssets.FindChecksum(sums, "vschromium-server-0.13.0-win-x64.zip"));
    Assert.IsNull(ReleaseAssets.FindChecksum(sums, "missing.zip"));
    Assert.AreEqual(hash, ReleaseAssets.FindChecksum(hash + "\n", "anything.vsix"));
  }

  [TestMethod]
  public void ParsesTheReleasesOfTheGitHubApi() {
    var json = JsonNode.Parse("""
      { "tag_name": "v0.13.0", "name": "0.13.0", "prerelease": true,
        "assets": [ { "name": "vschromium-server-0.13.0-win-x64.zip", "browser_download_url": "https://example.invalid/a.zip", "size": 1234 } ] }
      """)!.AsObject();
    var release = ServerLocator.ParseRelease(json);
    Assert.AreEqual("v0.13.0", release.Tag);
    Assert.IsTrue(release.Prerelease);
    Assert.AreEqual(1234, release.Assets.Single().Size);
  }

  [TestMethod]
  public void DetectsTheBenchFeaturesOfTheServer() {
    var old = ServerCapabilities.FromUsage("Usage: VsChromium.Server.exe --jsonrpc | --stdio [--cache-dir <directory> | --no-cache]");
    Assert.IsFalse(old.Bench);
    Assert.IsFalse(old.Threads);
    Assert.IsEmpty(old.ToList());
    var recent = ServerCapabilities.FromUsage("--jsonrpc [--bench] [--threads <n>] [--watcher-delay-ms <ms>] [--no-journal]")
      .WithInitializeResult(JsonNode.Parse("""{ "bench": { "requests": ["bench/saveCache", "bench/phases"] } }""")!.AsObject());
    Assert.IsTrue(recent.Bench && recent.Threads && recent.WatcherDelay && recent.NoJournal && recent.SaveCache && recent.Phases);
    CollectionAssert.AreEqual(new[] { "--bench", "--no-journal", "--threads", "--watcher-delay-ms", "bench/phases", "bench/saveCache" }, recent.ToList());
    // bench/* requests need --bench.
    var noBench = ServerCapabilities.FromUsage("--threads").WithInitializeResult(JsonNode.Parse("""{ "bench": { "requests": ["bench/saveCache"] } }""")!.AsObject());
    Assert.IsFalse(noBench.SaveCache);
  }
}

[TestClass]
public class InventoryParserTests {
  [TestMethod]
  public void DecodesTheSecurityCenterState() {
    Assert.AreEqual("on", InventoryCollector.DecodeAntivirusState(0x061000));
    Assert.AreEqual("off", InventoryCollector.DecodeAntivirusState(0x060100));
    Assert.AreEqual("snoozed", InventoryCollector.DecodeAntivirusState(0x062000));
  }

  [TestMethod]
  public void ParsesFltmc() {
    var output = """

      Filter Name                     Num Instances    Altitude    Frame
      ------------------------------  -------------  ------------  -----
      bindflt                                 1       409800         0
      WdFilter                               10       328010         0
      FileInfo                               10       40500          0
      """;
    CollectionAssert.AreEqual(new[] { "bindflt (altitude 409800)", "WdFilter (altitude 328010)", "FileInfo (altitude 40500)" }, InventoryCollector.ParseFltmc(output));
  }

  [TestMethod]
  public void ParsesDiskutilPlists() {
    var plist = """
      <?xml version="1.0" encoding="UTF-8"?>
      <plist version="1.0"><dict>
        <key>BusProtocol</key><string>Apple Fabric</string>
        <key>DeviceIdentifier</key><string>disk3s5</string>
        <key>FilesystemType</key><string>apfs</string>
        <key>SolidState</key><true/>
        <key>TotalSize</key><integer>994662584320</integer>
        <key>Partitions</key><array><string>ignored</string></array>
      </dict></plist>
      """;
    var values = InventoryCollector.ParsePlistDictionary(plist);
    Assert.AreEqual("apfs", values["FilesystemType"]);
    Assert.AreEqual("true", values["SolidState"]);
    Assert.AreEqual("994662584320", values["TotalSize"]);
    Assert.IsFalse(values.ContainsKey("Partitions"));
    Assert.IsEmpty(InventoryCollector.ParsePlistDictionary("not xml"));
  }

  [TestMethod]
  public void FindsTheDiskOfAPath() {
    var sep = Path.DirectorySeparatorChar;
    var root = Path.GetPathRoot(Path.GetTempPath())!;
    var data = Path.Combine(root, "data") + sep;
    var disks = new List<DiskInfo> { new() { MountPoint = root }, new() { MountPoint = data } };
    Assert.AreEqual(data, InventoryCollector.FindDisk(disks, Path.Combine(root, "data", "x"))?.MountPoint);
    Assert.AreEqual(root, InventoryCollector.FindDisk(disks, Path.Combine(root, "other"))?.MountPoint);
  }
}

[TestClass]
public class ResultTests {
  private static BenchResult Result(string label, string disk, double coldSeconds, double? searchMs) {
    var scenarios = new List<ScenarioResult> {
      new() { Name = "cold", Metrics = [Metric.Of("time to index", "s", [coldSeconds, coldSeconds + 2, coldSeconds - 1])] },
    };
    if (searchMs is { } ms) {
      scenarios.Add(new ScenarioResult { Name = "search", Metrics = [Metric.Of("regex", "ms", [ms])] });
    }
    return new BenchResult {
      Label = label,
      Machine = new MachineInfo { HostName = "host", CpuModel = "cpu", LogicalCores = 8 },
      Disk = new DiskInfo { MountPoint = disk, FileSystem = "NTFS", MediaType = "NVMe" },
      Corpus = new CorpusInfo { Name = "400k", FileCount = 400_000 },
      Server = new ServerInfo { Version = "1.2.3", Source = "release:v1.2.3" },
      Run = new RunInfo { Repeat = 3 },
      Scenarios = scenarios,
    };
  }

  [TestMethod]
  public void MetricsHaveMedianMinMax() {
    var metric = Metric.Of("x", "s", [3, 1, 2, 10]);
    Assert.AreEqual(2.5, metric.Median);
    Assert.AreEqual(1, metric.Min);
    Assert.AreEqual(10, metric.Max);
    Assert.AreEqual(2, Statistics.Median([2]));
    Assert.AreEqual(0, Statistics.Median([]));
  }

  [TestMethod]
  public void ResultsRoundTripThroughJson() {
    var result = Result("a", @"C:\", 10, 5);
    var copy = BenchResult.FromJson(result.ToJson());
    Assert.AreEqual(result.Label, copy.Label);
    Assert.AreEqual(10, copy.Scenarios[0].Metrics[0].Median);
    StringAssert.Contains(result.ToJson(), "\"scenarios\": [");
  }

  [TestMethod]
  public void ComparesResultsSideBySide() {
    var table = MarkdownReport.Compare([Result("C: NTFS", @"C:\", 20, 8), Result("D: Dev Drive", @"D:\", 10, null)]);
    var lines = table.Split('\n');
    StringAssert.StartsWith(lines[0], "| | C: NTFS | D: Dev Drive | ratio |");
    var cold = lines.Single(l => l.StartsWith("| cold: time to index", StringComparison.Ordinal));
    StringAssert.Contains(cold, "20.00 s | 10.00 s | 0.50x");
    var search = lines.Single(l => l.StartsWith("| search: regex", StringComparison.Ordinal));
    StringAssert.Contains(search, "8.0 ms | - | -");
    Assert.IsTrue(lines.Any(l => l.StartsWith("| Disk | C:\\ NTFS | D:\\ NTFS", StringComparison.Ordinal)));
  }

  [TestMethod]
  public void ComparisonLabelsAreUnique() {
    var labels = MarkdownReport.ColumnLabels([Result("same", @"C:\", 1, null), Result("same", @"C:\", 1, null), Result("", @"D:\", 1, null)]);
    CollectionAssert.AreEqual(new[] { "same", "same (2)", @"host D:\" }, labels);
  }

  [TestMethod]
  public void SummaryHasOneTablePerScenario() {
    var summary = MarkdownReport.Summary(Result("run", @"C:\", 12, 3) with { Scenarios = [.. Result("run", @"C:\", 12, 3).Scenarios, new ScenarioResult { Name = "threads", Skipped = true, Notes = ["no --threads"] }] });
    StringAssert.Contains(summary, "## cold");
    StringAssert.Contains(summary, "| time to index | 12.00 s | 11.00 s | 14.00 s |");
    StringAssert.Contains(summary, "## threads");
    StringAssert.Contains(summary, "- no --threads");
  }

  [TestMethod]
  public void FormatsUnits() {
    Assert.AreEqual("250 ms", MarkdownReport.Format(0.25, "s"));
    Assert.AreEqual("2.50 s", MarkdownReport.Format(2.5, "s"));
    Assert.AreEqual("1.5 GB", MarkdownReport.Format(1.5 * (1L << 30), "bytes"));
    Assert.AreEqual("12,345", MarkdownReport.Format(12345, "count"));
  }

  [TestMethod]
  public void ResultFileNamesAreSafe() {
    Assert.AreEqual("host-D-400k-20261106-153000", BenchApp.ResultBaseName("host", @"D:\", "400k", new DateTime(2026, 11, 6, 15, 30, 0)));
    Assert.AreEqual("host-root-1M-20261106-153000", BenchApp.ResultBaseName("host", "/", "1M", new DateTime(2026, 11, 6, 15, 30, 0)));
    Assert.AreEqual("host-mnt-data-2M-20261106-153000", BenchApp.ResultBaseName("host", "/mnt/data/", "2M", new DateTime(2026, 11, 6, 15, 30, 0)));
  }
}
