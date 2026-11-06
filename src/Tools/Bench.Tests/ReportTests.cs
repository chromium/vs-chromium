// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using VsChromium.Bench.Corpora;
using VsChromium.Bench.Inventory;
using VsChromium.Bench.Reports;
using VsChromium.Bench.Results;
using VsChromium.Bench.Shared.Corpus;

namespace VsChromium.Bench.Tests;

[TestClass]
public class ScrubberTests {
  private static readonly List<SensitiveTerm> Terms = [
    new(@"C:\Users\JaneDoe", "<user-profile>", IsPath: true),
    new(@"D:\vschromium-corpora\large-1-400000-v2", "<corpus-root>", IsPath: true),
    new(@"D:\vschromium-corpora", "<corpora-dir>", IsPath: true),
    new("JANE-WORKSTATION", "<machine>", IsPath: false),
    new("CONTOSO", "<domain>", IsPath: false),
    new("JaneDoe", "<user>", IsPath: false),
  ];

  [TestMethod]
  [DataRow(@"C:\Users\JaneDoe\bench\vschromium-bench.exe", "<user-profile>\\bench\\vschromium-bench.exe")]
  [DataRow(@"c:/users/janedoe/bench", "<user-profile>/bench")]
  [DataRow(@"C:\\Users\\JaneDoe\\AppData", "<user-profile>\\\\AppData")]
  [DataRow(@"D:\VSCHROMIUM-CORPORA\large-1-400000-v2\src", "<corpus-root>\\src")]
  [DataRow(@"d:/vschromium-corpora/bench-cache", "<corpora-dir>/bench-cache")]
  [DataRow("host jane-workstation, user janedoe@contoso", "host <machine>, user <user>@<domain>")]
  [DataRow("JANEDOE ran on Jane-Workstation", "<user> ran on <machine>")]
  public void ReplacesNamesAndPathsInAnyCasingAndSlashStyle(string input, string expected) {
    Assert.AreEqual(expected, Scrubber.Scrub(input, Terms));
  }

  [TestMethod]
  public void ScrubsPathsInsideTheCommandLine() {
    var line = @"""C:\Users\JaneDoe\bench\vschromium-bench.exe"" run --corpora-dir D:\vschromium-corpora --server C:\Users\JaneDoe\src\Binaries\Release\Server\VsChromium.Server.exe";
    var scrubbed = Scrubber.ScrubAndVerify(line, Terms);
    Assert.AreEqual(@"""<user-profile>\bench\vschromium-bench.exe"" run --corpora-dir <corpora-dir> --server <user-profile>\src\Binaries\Release\Server\VsChromium.Server.exe", scrubbed);
  }

  [TestMethod]
  public void DoesNotReplaceInsideLongerWords() {
    var terms = new List<SensitiveTerm> { new("dev", "<user>", IsPath: false) };
    Assert.AreEqual("devices, developer, <user>, <user>-tools", Scrubber.Scrub("devices, developer, dev, DEV-tools", terms));
    // Too short terms are ignored (they would match ordinary words).
    Assert.AreEqual("an id", Scrubber.Scrub("an id", [new SensitiveTerm("id", "<user>", IsPath: false)]));
  }

  [TestMethod]
  public void FindsWhatIsLeftAndRefusesTheText() {
    // A term without replacement effect: the verification of an unscrubbed text.
    var found = Scrubber.Find(@"report of jane-workstation in c:/Users/JaneDoe, S-1-5-21-1004336348-1177238915-682003330-512, 3f2504e0-4f89-11d3-9a0c-0305e82c3301", Terms);
    Assert.IsTrue(found.Any(f => f.StartsWith("<machine>", StringComparison.Ordinal)));
    Assert.IsTrue(found.Any(f => f.StartsWith("<user-profile>", StringComparison.Ordinal)));
    Assert.IsTrue(found.Any(f => f.StartsWith("SID", StringComparison.Ordinal)));
    Assert.IsTrue(found.Any(f => f.StartsWith("GUID", StringComparison.Ordinal)));
    var error = Assert.ThrowsExactly<SensitiveDataException>(() => Scrubber.ScrubAndVerify("SID S-1-5-21-1-2-3", Terms));
    Assert.HasCount(1, error.Found);
    Assert.IsEmpty(Scrubber.Find("version 10.0.26200.9711, driver 32.0.21041.1000, id 0029b307", Terms));
  }

  [TestMethod]
  public void CurrentMachineTermsIncludeUserMachineAndProfile() {
    var terms = Scrubber.CurrentMachineTerms([(@"X:\corpora", "<corpora-dir>")]);
    Assert.IsTrue(terms.Any(t => t.Value == Environment.UserName && t.Replacement == "<user>"));
    Assert.IsTrue(terms.Any(t => t.Value == Environment.MachineName && t.Replacement == "<machine>"));
    Assert.IsTrue(terms.Any(t => t.Replacement == "<user-profile>"));
    Assert.IsTrue(terms.Any(t => t.Replacement == "<corpora-dir>"));
    var text = $"{Environment.UserName.ToUpperInvariant()} on {Environment.MachineName.ToLowerInvariant()} in {Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).Replace('\\', '/')}";
    var scrubbed = Scrubber.ScrubAndVerify(text, terms);
    StringAssert.Contains(scrubbed, "<user-profile>");
  }
}

[TestClass]
public class MachineIdentityTests {
  [TestMethod]
  public void IdIsCreatedOnceAndPersisted() {
    var dir = Path.Combine(Path.GetTempPath(), "VsChromiumBenchId-" + Guid.NewGuid().ToString("N"));
    try {
      var first = MachineIdentity.GetOrCreate(dir);
      var second = MachineIdentity.GetOrCreate(dir);
      Assert.AreEqual(first, second);
      Assert.AreNotEqual(Guid.Empty, first);
      Assert.AreEqual(first.ToString("N")[..8], MachineIdentity.Short(first));
      Assert.AreEqual(8, MachineIdentity.Short(first.ToString("D")).Length);
      // Another data directory: another (random, not hardware derived) id.
      var other = Path.Combine(dir, "other");
      Assert.AreNotEqual(first, MachineIdentity.GetOrCreate(other));
      // An invalid file is replaced.
      File.WriteAllText(Path.Combine(dir, MachineIdentity.FileName), "garbage");
      var replaced = MachineIdentity.GetOrCreate(dir);
      Assert.AreNotEqual(first, replaced);
      Assert.AreEqual(replaced, MachineIdentity.GetOrCreate(dir));
    } finally {
      Directory.Delete(dir, recursive: true);
    }
  }
}

[TestClass]
public class ShareableReportTests {
  private const string Host = "JANE-WORKSTATION";

  /// <summary>A results file as written by a run, with identifying data everywhere it can be.</summary>
  private static BenchResult SampleResult() => new() {
    ToolVersion = "1.0.0",
    StartedUtc = new DateTime(2026, 11, 6, 15, 56, 46, DateTimeKind.Utc),
    EndedUtc = new DateTime(2026, 11, 6, 15, 59, 54, DateTimeKind.Utc),
    Label = $"{Host} D: ReFS Dev Drive",
    MachineId = "0029b307-1111-4222-8333-444455556666",
    Machine = new MachineInfo {
      HostName = Host, Os = "Windows", OsDescription = "Windows 11 Pro 24H2 (build 26100.4061)", Architecture = "x64",
      CpuModel = "Example CPU 12-Core", LogicalCores = 24, PhysicalCores = 12, BaseClockMHz = 4700, PhysicalMemoryBytes = 128L << 30,
      MemoryType = "DDR5", MemorySpeedMTs = 5200, Gpus = [new GpuInfo { Name = "Example GPU", DriverVersion = "32.0.21041.1000" }],
      PowerPlan = "Balanced", BenchRuntime = ".NET 10.0.14 (NativeAOT)",
    },
    Disk = new DiskInfo { MountPoint = @"D:\", Label = "JaneDevDrive", FileSystem = "ReFS", DevDrive = true, DevDriveTrusted = true,
                          TotalBytes = 1000L << 30, FreeBytes = 900L << 30, MediaType = "NVMe", BusType = "NVMe", Device = "Example NVMe 1TB",
                          ChangeJournal = "inactive" },
    Disks = [
      new DiskInfo { MountPoint = @"C:\", Label = "Jane's Windows", FileSystem = "NTFS", DevDrive = false, TotalBytes = 2000L << 30,
                     FreeBytes = 500L << 30, MediaType = "NVMe", BusType = "NVMe", Device = "Example NVMe 2TB", ChangeJournal = "active",
                     ChangeJournalMaxBytes = 32L << 20 },
      new DiskInfo { MountPoint = @"D:\", Label = "JaneDevDrive", FileSystem = "ReFS", DevDrive = true, DevDriveTrusted = true,
                     TotalBytes = 1000L << 30, FreeBytes = 900L << 30, MediaType = "NVMe", BusType = "NVMe", Device = "Example NVMe 1TB",
                     ChangeJournal = "inactive" },
      new DiskInfo { MountPoint = @"Z:\", FileSystem = "NTFS", MediaType = "Network", TotalBytes = 1L << 40, FreeBytes = 1L << 39 },
    ],
    Security = new SecurityInfo { Antivirus = [new AntivirusProduct { Name = "Example Antivirus", State = "on" }], DefenderMode = "Passive Mode",
                                  DefenderRealTimeProtection = false, FilterDrivers = ["ExampleFilter (altitude 320000)"] },
    Corpus = new CorpusInfo { Name = "400k", Path = @"D:\vschromium-corpora\large-1-400000-v2", GeneratorVersion = 2, FileCount = 400_000,
                              DirectoryCount = 86_864, TotalBytes = 3_912_356_729, SearchableFileCount = 367_544,
                              FilesPerDirectoryHistogram = [new FilesPerDirectoryBucket("0", 22_150, 0, 25.5, 0), new FilesPerDirectoryBucket("1", 33_536, 33_536, 38.6, 8.6)] },
    Server = new ServerInfo { Source = @"C:\Users\JaneDoe\src\vs-chromium\Binaries\Release\Server\VsChromium.Server.exe",
                              Path = @"C:\Users\JaneDoe\src\vs-chromium\Binaries\Release\Server\VsChromium.Server.exe", Version = "0.9.39", ProtocolVersion = 1,
                              Runtime = ".NET 10.0.12 X64 (Microsoft Windows 10.0.26100)" },
    Run = new RunInfo {
      Scenarios = ["cold", "restart"], Repeat = 2, CacheMode = "warm", ChangeCounts = [1, 1000], KeepServer = true,
      Arguments = ["run", "--non-interactive", "--disk", "D:", "--corpora-dir", @"D:\vschromium-corpora", "--corpus", "400k",
                   "--server", @"C:\Users\JaneDoe\src\vs-chromium\Binaries\Release\Server\VsChromium.Server.exe", "--repeat", "2",
                   "--results-dir", @"C:\Users\JaneDoe\bench results", "--label", $"{Host} run", "--rerun-script", "none"],
    },
    Scenarios = [
      new ScenarioResult { Name = "cold", Metrics = [Metric.Of("time to index", "s", [6.52, 6.56])] },
      new ScenarioResult { Name = "restart", Metrics = [Metric.Of("time to verified", "s", [2.3, 2.8])],
                           Info = { ["fallback reason"] = @"the change journal is not active on ""D:\""" },
                           Error = @"Cannot read C:\Users\JaneDoe\AppData\Local\Temp\x.json" },
    ],
  };

  private static List<SensitiveTerm> SampleTerms(BenchResult r) {
    var terms = ShareableReport.TermsOf(r, []);
    // The terms of the machine that produced the sample.
    terms.Add(new SensitiveTerm(@"C:\Users\JaneDoe", "<user-profile>", IsPath: true));
    terms.Add(new SensitiveTerm("JaneDoe", "<user>", IsPath: false));
    terms.Add(new SensitiveTerm("CONTOSO", "<domain>", IsPath: false));
    return terms;
  }

  [TestMethod]
  public void ReportHasTheSpecsAndResultsButNoIdentifyingData() {
    var result = SampleResult();
    var options = new ReportOptions { MachineId = result.MachineId!, MachineLabel = "lab desktop" };
    var text = Scrubber.ScrubAndVerify(ShareableReport.Render(result, options), SampleTerms(result));

    StringAssert.StartsWith(text, "# vschromium-bench: 2026-11-06 0029b307 D: (ReFS, Dev Drive, trusted, NVMe) 400k\n");
    StringAssert.Contains(text, "| Date (UTC) | 2026-11-06 15:56 (3 minutes) |");
    StringAssert.Contains(text, "| Machine id | `0029b307` ");
    StringAssert.Contains(text, "| Machine label | lab desktop |");
    StringAssert.Contains(text, "| Server | 0.9.39 (local build), protocol 1 |");
    StringAssert.Contains(text, "| OS | Windows 11 Pro 24H2 (build 26100.4061) (x64) |");
    StringAssert.Contains(text, "| Cores | 12 physical, 24 logical processors |");
    StringAssert.Contains(text, "| Clock | base 4.7 GHz |");
    StringAssert.Contains(text, "| RAM | 128.0 GB (DDR5 5200 MT/s) |");
    StringAssert.Contains(text, "| GPU | Example GPU (driver 32.0.21041.1000) |");
    StringAssert.Contains(text, "| C: | NTFS | no | 2.0 TB | 500 GB | NVMe | Example NVMe 2TB | NVMe | active (max 32.0 MB) |");
    StringAssert.Contains(text, "| D: **(test)** | ReFS | yes, trusted | 1,000 GB | 900 GB | NVMe | Example NVMe 1TB | NVMe | inactive |");
    Assert.DoesNotContain("Z:", text, "network drives are not listed");
    StringAssert.Contains(text, "| Antivirus | Example Antivirus (on) |");
    StringAssert.Contains(text, "| Filter drivers | ExampleFilter (altitude 320000) |");
    StringAssert.Contains(text, "| 1 | 33,536 | 38.6 | 33,536 | 8.6 |");
    StringAssert.Contains(text, "### cold");
    StringAssert.Contains(text, "| time to index | 6.54 s | 6.52 s | 6.56 s |");
    StringAssert.Contains(text, "<bench> run --non-interactive --disk D: --corpora-dir <corpora-dir> --corpus 400k --server <server> --repeat 2 " +
                                "--results-dir <results-dir> --label <label> --rerun-script none");
    StringAssert.Contains(text, @"**Error:** Cannot read <user-profile>\AppData\Local\Temp\x.json");

    // No identifying data: names, paths, volume labels.
    foreach (var forbidden in new[] { "JANE", "Jane", "jane", "CONTOSO", "JaneDevDrive", @"vschromium-corpora\", @"C:\Users", "0029b307-1111" }) {
      Assert.DoesNotContain(forbidden, text, $"\"{forbidden}\" in the report");
    }
  }

  [TestMethod]
  public void SecurityCanBeRedacted() {
    var result = SampleResult();
    var text = ShareableReport.Render(result, new ReportOptions { MachineId = result.MachineId!, RedactSecurity = true });
    Assert.DoesNotContain("Example Antivirus", text);
    Assert.DoesNotContain("ExampleFilter", text);
    StringAssert.Contains(text, "Not included (--redact-security).");
  }

  [TestMethod]
  public void FileNameAndDiskSummary() {
    var result = SampleResult();
    Assert.AreEqual("2026-11-06-0029b307-refs-devdrive-400k.md", ShareableReport.FileName(result, new ReportOptions { MachineId = result.MachineId! }));
    Assert.AreEqual("D: (ReFS, Dev Drive, trusted, NVMe)", ShareableReport.DiskSummary(result.Disk!));
    Assert.AreEqual("/mnt/data", ShareableReport.Mount("/mnt/data/"));
    Assert.AreEqual("/", ShareableReport.Mount("/"));
  }

  [TestMethod]
  public void WriteRefusesALeftOverTermAndReplacesTheReportOfTheSameRun() {
    var dir = Path.Combine(Path.GetTempPath(), "VsChromiumBenchReport-" + Guid.NewGuid().ToString("N"));
    try {
      var result = SampleResult();
      var options = new ReportOptions { MachineId = result.MachineId! };
      // A machine label with a GUID: refused, nothing written.
      Assert.ThrowsExactly<SensitiveDataException>(() =>
        ShareableReport.Write(result, options with { MachineLabel = "3f2504e0-4f89-11d3-9a0c-0305e82c3301" }, dir, []));
      Assert.IsFalse(Directory.Exists(dir) && Directory.EnumerateFiles(dir).Any());

      var first = ShareableReport.Write(result, options, dir, [(@"C:\Users\JaneDoe", "<user-profile>")]);
      var again = ShareableReport.Write(result, options, dir, [(@"C:\Users\JaneDoe", "<user-profile>")]);
      Assert.AreEqual(first, again, "same run: replaced");
      var other = ShareableReport.Write(result with { StartedUtc = result.StartedUtc.AddHours(1) }, options, dir, [(@"C:\Users\JaneDoe", "<user-profile>")]);
      Assert.AreEqual(Path.Combine(dir, "2026-11-06-0029b307-refs-devdrive-400k-2.md"), other);
    } finally {
      if (Directory.Exists(dir)) {
        Directory.Delete(dir, recursive: true);
      }
    }
  }

  [TestMethod]
  [DataRow("AMD Radeon Pro W6400", null, false)]
  [DataRow("NVIDIA GeForce RTX 4090", @"PCI\VEN_10DE&DEV_2684&SUBSYS_00000000&REV_A1\4&1A2B3C4D&0&0008", false)]
  [DataRow("Intel(R) UHD Graphics 770", @"PCI\VEN_8086&DEV_4680", false)]
  [DataRow("Jump Desktop Virtual Monitor", null, true)]
  [DataRow("Microsoft Remote Display Adapter", null, true)]
  [DataRow("Microsoft Basic Display Adapter", null, true)]
  [DataRow("Microsoft Hyper-V Video", null, true)]
  [DataRow("VMware SVGA 3D", null, true)]
  [DataRow("VirtualBox Graphics Adapter (WDDM)", null, true)]
  [DataRow("Parallels Display Adapter (WDDM)", null, true)]
  [DataRow("Citrix Indirect Display Adapter", null, true)]
  [DataRow("Parsec Virtual Display Adapter", null, true)]
  [DataRow("Some Unknown Display", @"ROOT\DISPLAY\0000", true)]
  [DataRow("Some Unknown Display", @"SWD\REMOTEDISPLAYENUM\1&2", true)]
  [DataRow("Red Hat QXL controller", null, true)]
  [DataRow("Cirrus Logic GD 5446", null, true)]
  public void DetectsVirtualDisplayAdapters(string name, string? pnpDeviceId, bool expected) {
    Assert.AreEqual(expected, InventoryCollector.IsVirtualDisplay(name, pnpDeviceId));
  }

  [TestMethod]
  public void VirtualDisplaysAreNotNamedByDefault() {
    var result = SampleResult() with { };
    result = result with {
      Machine = result.Machine with {
        Gpus = [.. result.Machine.Gpus, new GpuInfo { Name = "Jump Desktop Virtual Monitor", DriverVersion = "10.15.21.0", IsVirtual = true },
                new GpuInfo { Name = "Microsoft Remote Display Adapter" /* old results file: no flag, detected by name */ }],
        RemoteSession = true,
      },
    };
    var text = ShareableReport.Render(result, new ReportOptions { MachineId = result.MachineId! });
    StringAssert.Contains(text, "| GPU | Example GPU (driver 32.0.21041.1000) |");
    StringAssert.Contains(text, "| Remote session software | detected |");
    StringAssert.Contains(text, "| Remote session | yes |");
    Assert.DoesNotContain("Jump", text);
    Assert.DoesNotContain("Remote Display Adapter", text);

    var named = ShareableReport.Render(result, new ReportOptions { MachineId = result.MachineId!, IncludeVirtualDisplays = true });
    StringAssert.Contains(named, "Jump Desktop Virtual Monitor (driver 10.15.21.0) (virtual)");
    Assert.DoesNotContain("Remote session software", named);

    var physicalOnly = ShareableReport.Render(SampleResult(), new ReportOptions { MachineId = result.MachineId! });
    Assert.DoesNotContain("Remote session software", physicalOnly);
    StringAssert.Contains(physicalOnly, "| Remote session | - |");
  }

  [TestMethod]
  public void SymlinksAreOnByDefaultAndCanBeDisabled() {
    Assert.IsNull(Cli.CommandLineParser.Parse(["run"]).Symlinks, "default: when the OS allows them");
    Assert.IsFalse(Cli.CommandLineParser.Parse(["run", "--no-symlinks"]).Symlinks);
    Assert.IsTrue(Cli.CommandLineParser.Parse(["run", "--include-virtual-displays"]).IncludeVirtualDisplays);
    CollectionAssert.Contains(Cli.RerunScript.ToArguments(new Cli.BenchOptions { Symlinks = false }), "--no-symlinks");
    CollectionAssert.DoesNotContain(Cli.RerunScript.ToArguments(new Cli.BenchOptions { Symlinks = null }), "--no-symlinks");
    // Corpora with and without links are separate directories (never reused for one another).
    Assert.AreNotEqual(CorpusCatalog.Spec("400k", true).DirectoryName, CorpusCatalog.Spec("400k", false).DirectoryName);
  }

  [TestMethod]
  public void ParsesTheReportCommand() {
    var options = Cli.CommandLineParser.Parse(["report", "a.json", "b.json", "--report-dir", "docs/benchmarks/results", "--machine-label", "lab desktop", "--redact-security"]);
    Assert.AreEqual(Cli.BenchCommand.Report, options.Command);
    CollectionAssert.AreEqual(new[] { "a.json", "b.json" }, options.Files.ToArray());
    Assert.AreEqual("docs/benchmarks/results", options.ReportDirectory);
    Assert.AreEqual("lab desktop", options.MachineLabel);
    Assert.IsTrue(options.RedactSecurity);
    Assert.ThrowsExactly<Cli.CommandLineException>(() => Cli.CommandLineParser.Parse(["report"]));
    // The report options of a run are in its rerun command line.
    var args = Cli.RerunScript.ToArguments(new Cli.BenchOptions { MachineLabel = "lab", RedactSecurity = true, ReportDirectory = "r" });
    CollectionAssert.IsSubsetOf(new[] { "--machine-label", "lab", "--redact-security", "--report-dir", "r" }, args);
  }

  [TestMethod]
  public void ReportOfAResultsFileFromJson() {
    var result = BenchResult.FromJson(SampleResult().ToJson());
    var text = Scrubber.ScrubAndVerify(ShareableReport.Render(result, new ReportOptions { MachineId = result.MachineId! }), SampleTerms(result));
    StringAssert.Contains(text, "| GPU | Example GPU (driver 32.0.21041.1000) |");
    Assert.DoesNotContain(Host, text);
  }
}

[TestClass]
public class FileChangesTests {
  /// <summary>Files seen through symbolic links are never modified (their links would keep the old contents in the index).</summary>
  [TestMethod]
  public void LinkTargetsAreNotModified() {
    var root = Path.Combine(Path.GetTempPath(), "VsChromiumBenchChanges-" + Guid.NewGuid().ToString("N"));
    try {
      Directory.CreateDirectory(Path.Combine(root, "a"));
      Directory.CreateDirectory(Path.Combine(root, "linked"));
      Directory.CreateDirectory(Path.Combine(root, "out"));
      File.WriteAllText(Path.Combine(root, "a", "plain.cc"), "x");
      File.WriteAllText(Path.Combine(root, "a", "target.cc"), "x");
      File.WriteAllText(Path.Combine(root, "linked", "inside.cc"), "x");
      File.WriteAllText(Path.Combine(root, "out", "build.cc"), "x");
      if (CodeBaseGenerator.CanCreateSymlinks(root) is { } reason) {
        Assert.Inconclusive($"Symbolic links not available: {reason}");
      }
      File.CreateSymbolicLink(Path.Combine(root, "a", "target_link.cc"), "target.cc");
      Directory.CreateSymbolicLink(Path.Combine(root, "a", "linked_dirlink"), Path.Combine("..", "linked"));
      var changes = Scenarios.FileChanges.Scan(root);
      Assert.AreEqual(1, changes.CandidateCount);
      CollectionAssert.AreEqual(new[] { Path.Combine(root, "a", "plain.cc") }, changes.Select(10, salt: 1));
    } finally {
      Directory.Delete(root, recursive: true);
    }
  }
}
