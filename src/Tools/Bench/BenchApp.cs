// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using VsChromium.Bench.Cli;
using VsChromium.Bench.Corpora;
using VsChromium.Bench.Inventory;
using VsChromium.Bench.Platform;
using VsChromium.Bench.Reports;
using VsChromium.Bench.Results;
using VsChromium.Bench.Scenarios;
using VsChromium.Bench.Servers;
using VsChromium.Bench.Shared.Corpus;

namespace VsChromium.Bench;

/// <summary>The commands of the benchmark app (see <see cref="CommandLineParser.Usage"/>).</summary>
public sealed class BenchApp(BenchOptions options, Prompt prompt, TextWriter output) {
  private readonly Stopwatch _clock = Stopwatch.StartNew();

  public static string Version =>
    typeof(BenchApp).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0] ?? "0.0";

  private bool Interactive => !options.NonInteractive;

  private string DataDirectory => options.DataDirectory ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create), "VsChromium", "Bench");

  private void Log(string message) => output.WriteLine($"[{_clock.Elapsed:hh\\:mm\\:ss}] {message}");

  public async Task<int> RunAsync(CancellationToken cancellation) {
    switch (options.Command) {
      case BenchCommand.Help:
        output.WriteLine(CommandLineParser.Usage);
        return 0;
      case BenchCommand.Inventory:
        return InventoryCommand();
      case BenchCommand.Compare:
        return CompareCommand();
      case BenchCommand.Report:
        return ReportCommand();
      case BenchCommand.Cleanup:
        return CleanupCommand();
      default:
        return await RunCommandAsync(cancellation);
    }
  }

  // -----------------------------------------------------------------------------------------------
  // inventory

  /// <summary>Answers the question of the elevated details (filter drivers, Dev Drive details).</summary>
  private bool AskElevatedDetails(bool elevated) {
    if (!elevated) {
      return false;
    }
    return options.ListFilters switch {
      YesNoAsk.Yes => true,
      YesNoAsk.No => false,
      _ => Interactive && prompt.YesNo("Running as admin: list the file system filter drivers (fltmc) and the Dev Drive details (fsutil)?", true),
    };
  }

  private int InventoryCommand() {
    var elevated = InventoryCollector.IsElevated();
    var inventory = InventoryCollector.Collect(AskElevatedDetails(elevated));
    if (options.Json) {
      output.WriteLine(JsonSerializer.Serialize(inventory, ResultsJsonContext.Default.MachineInventory));
    } else {
      PrintInventory(inventory);
    }
    return 0;
  }

  private void PrintInventory(MachineInventory inventory) {
    var m = inventory.Machine;
    output.WriteLine($"Machine:   {m.HostName}, {m.OsDescription} ({m.Architecture})");
    output.WriteLine($"CPU:       {m.CpuModel}, {m.LogicalCores} threads, {DiskInfo.FormatBytes(m.PhysicalMemoryBytes)} RAM" +
                     (m.PowerPlan != null ? $", power plan {m.PowerPlan}" : ""));
    output.WriteLine($"Bench:     {Version}, {m.BenchRuntime}{(m.IsElevated ? ", elevated" : "")}");
    var s = inventory.Security;
    output.WriteLine($"Antivirus: {(s.Antivirus.Count > 0 ? string.Join(", ", s.Antivirus.Select(a => $"{a.Name} ({a.State})")) : s.AntivirusNote)}");
    if (OperatingSystem.IsWindows()) {
      output.WriteLine($"Defender:  {(s.DefenderRealTimeProtection is { } rt ? $"{s.DefenderMode}, real-time protection {(rt ? "on" : "off")}" : s.DefenderNote)}");
      output.WriteLine($"Filters:   {(s.FilterDrivers is { } f ? string.Join(", ", f) : s.FilterDriversNote)}");
    }
    output.WriteLine("Disks:");
    foreach (var d in inventory.Disks) {
      output.WriteLine($"  {d.Summary}{(d.Device != null ? $" [{d.Device}]" : "")}");
      foreach (var note in d.Notes) {
        output.WriteLine($"      {note}");
      }
    }
  }

  // -----------------------------------------------------------------------------------------------
  // compare

  private int CompareCommand() {
    var files = new List<string>();
    foreach (var item in options.Files) {
      if (Directory.Exists(item)) {
        files.AddRange(Directory.EnumerateFiles(item, "*.json").Order(StringComparer.Ordinal));
      } else if (File.Exists(item)) {
        files.Add(item);
      } else {
        output.WriteLine($"Not found: {item}");
        return 2;
      }
    }
    var results = files.Select(f => BenchResult.FromJson(File.ReadAllText(f))).ToList();
    var markdown = MarkdownReport.Compare(results);
    output.WriteLine(markdown);
    if (options.Output != null) {
      File.WriteAllText(options.Output, markdown);
      output.WriteLine($"Written to {options.Output}");
    }
    return 0;
  }

  // -----------------------------------------------------------------------------------------------
  // report

  private int ReportCommand() {
    var failed = false;
    foreach (var file in options.Files) {
      if (!File.Exists(file)) {
        output.WriteLine($"Not found: {file}");
        return 2;
      }
      var result = BenchResult.FromJson(File.ReadAllText(file));
      var directory = options.ReportDirectory ?? Path.GetDirectoryName(Path.GetFullPath(file))!;
      failed |= !WriteShareableReport(result, directory, [(Path.GetDirectoryName(Path.GetFullPath(file))!, "<results-dir>")]);
    }
    return failed ? 1 : 0;
  }

  /// <summary>Writes the shareable report of a result (scrubbed and verified); false if it was refused.</summary>
  private bool WriteShareableReport(BenchResult result, string directory, IEnumerable<(string Path, string Placeholder)> extraPaths) {
    var machineId = result.MachineId ?? MachineIdentity.GetOrCreate(DataDirectory).ToString("D");
    var label = options.MachineLabel ?? result.MachineLabel;
    if (!string.IsNullOrWhiteSpace(label)) {
      output.WriteLine($"Note: the machine label \"{label}\" is included verbatim in the shareable report.");
    }
    var paths = new List<(string, string)>(extraPaths) {
      (Path.GetFullPath(directory), "<report-dir>"),
      (DataDirectory, "<data-dir>"),
      (Environment.CurrentDirectory, "<current-dir>"),
    };
    if (Environment.ProcessPath is { } benchPath) {
      paths.Add((benchPath, "<bench>"));
      paths.Add((Path.GetDirectoryName(benchPath)!, "<bench-dir>"));
    }
    try {
      var path = ShareableReport.Write(result, new ReportOptions {
        MachineId = machineId,
        MachineLabel = label,
        RedactSecurity = options.RedactSecurity,
        IncludeVirtualDisplays = options.IncludeVirtualDisplays,
      }, directory, paths);
      Log($"Shareable report (no personal data, can be committed to docs/benchmarks/results/): {path}");
      return true;
    } catch (SensitiveDataException e) {
      output.WriteLine($"Error: {e.Message}");
      return false;
    }
  }

  // -----------------------------------------------------------------------------------------------
  // cleanup

  private int CleanupCommand() {
    var targets = options.CleanupTargets ?? ["corpora", "caches"];
    var all = targets.Contains("all");
    var disks = InventoryCollector.CollectDisks();
    var corporaDirs = new List<string>();
    if (options.CorporaDirectory != null) {
      corporaDirs.Add(options.CorporaDirectory);
    } else if (options.Disk != null) {
      var disk = InventoryCollector.FindDiskByName(disks, options.Disk) ?? throw new CommandLineException($"Unknown disk \"{options.Disk}\".");
      corporaDirs.Add(CorpusCatalog.DefaultCorporaDirectory(disk.MountPoint));
    } else {
      corporaDirs.AddRange(disks.Select(d => CorpusCatalog.DefaultCorporaDirectory(d.MountPoint)));
    }
    var deletions = new List<string>();
    foreach (var dir in corporaDirs.Where(Directory.Exists)) {
      foreach (var entry in Directory.EnumerateFileSystemEntries(dir)) {
        var name = Path.GetFileName(entry);
        var isCache = name.StartsWith("bench-cache-", StringComparison.Ordinal);
        var isCorpus = !isCache && CorpusSpec.All.Any(s => name.StartsWith(s.Name + "-", StringComparison.Ordinal));
        if ((isCache && (all || targets.Contains("caches"))) || (isCorpus && (all || targets.Contains("corpora")))) {
          deletions.Add(entry);
        }
      }
    }
    if (all || targets.Contains("servers")) {
      var servers = Path.Combine(DataDirectory, "servers");
      if (Directory.Exists(servers)) {
        deletions.Add(servers);
      }
    }
    if (all || targets.Contains("results")) {
      var results = Path.GetFullPath(options.ResultsDirectory ?? "results");
      if (Directory.Exists(results)) {
        deletions.Add(results);
      }
    }
    if (deletions.Count == 0) {
      output.WriteLine("Nothing to delete.");
      return 0;
    }
    output.WriteLine("To delete:");
    foreach (var path in deletions) {
      output.WriteLine($"  {path}");
    }
    if (!options.Yes) {
      if (!Interactive) {
        output.WriteLine("Add --yes to delete without confirmation (non-interactive).");
        return 1;
      }
      if (!prompt.YesNo("Delete?", false)) {
        return 1;
      }
    }
    foreach (var path in deletions) {
      if (Directory.Exists(path)) {
        Directory.Delete(path, recursive: true);
      } else {
        File.Delete(path);
      }
      output.WriteLine($"Deleted {path}");
    }
    return 0;
  }

  // -----------------------------------------------------------------------------------------------
  // run

  private async Task<int> RunCommandAsync(CancellationToken cancellation) {
    var resolved = options;
    var startedUtc = DateTime.UtcNow;
    output.WriteLine($"vschromium-bench {Version}");

    // 1. Inventory.
    var elevated = InventoryCollector.IsElevated();
    var elevatedDetails = AskElevatedDetails(elevated);
    if (elevated) {
      resolved = resolved with { ListFilters = elevatedDetails ? YesNoAsk.Yes : YesNoAsk.No };
    }
    Log("Machine and disk inventory...");
    var inventory = InventoryCollector.Collect(elevatedDetails);
    PrintInventory(inventory);

    // 2. Disk.
    var disk = SelectDisk(inventory.Disks, ref resolved);
    var corporaDir = resolved.CorporaDirectory ?? CorpusCatalog.DefaultCorporaDirectory(disk.MountPoint);

    // 3. Corpus.
    var spec = SelectCorpus(disk, corporaDir, ref resolved, out var symlinkNote);
    var existing = CorpusCache.TryGet(corporaDir, spec);
    var estimate = CorpusCatalog.Estimate(spec, existing != null);
    if (!estimate.Fits(disk.FreeBytes)) {
      output.WriteLine($"Not enough free space on {disk.MountPoint}: {DiskInfo.FormatBytes(disk.FreeBytes)} free, " +
                       $"{DiskInfo.FormatBytes(estimate.RequiredFreeBytes)} needed (corpus and index cache, +10%).");
      return 3;
    }

    // 4. Server.
    var server = await SelectServerAsync(resolved, cancellation);
    // Note: Local servers are recorded by path (the rerun uses exactly the same executable).
    resolved = resolved with { Server = server.Source is "repo" or "vscode" ? server.Path : server.Source };
    var capabilities = ServerCapabilities.Probe(server.Path);
    Log($"Server: {server.Description}; bench features: {(capabilities.ToList() is { Count: > 0 } c ? string.Join(", ", c) : "none (existing protocol only)")}");

    // 5. Scenarios, repeats, file cache.
    resolved = SelectScenarios(resolved);
    var (flush, cacheNote) = SelectCacheMode(elevated, ref resolved);
    var label = resolved.Label ?? DefaultLabel(inventory.Machine, disk);

    // 6. Corpus generation (or reuse).
    Log($"Corpus {spec.DirectoryName} in {corporaDir}");
    var corpus = CorpusCache.GetOrCreate(corporaDir, spec, Log);

    // 7. Scenarios.
    var cacheDirectory = Path.Combine(corporaDir, "bench-cache-" + spec.DirectoryName);
    List<ScenarioResult> scenarios;
    string serverVersion = "", serverRuntime = "";
    var protocolVersion = 0;
    RunContext context;
    try {
      // The version of the server, and its "bench/*" requests (listed by "server/initialize").
      await using (var probe = await ServerHost.StartAsync(server.Path, null, capabilities, _clock)) {
        serverVersion = (string?)probe.Client.InitializeResult["serverVersion"] ?? "";
        protocolVersion = (int?)probe.Client.InitializeResult["protocolVersion"] ?? 0;
        serverRuntime = (string?)(await probe.StatisticsAsync())["runtimeDescription"] ?? "";
        capabilities = probe.Capabilities;
      }
      context = new RunContext(corpus, server.Path, capabilities, cacheDirectory, resolved.Scenarios!, resolved.Repeat!.Value, flush,
                               resolved.ChangeCounts ?? BenchOptions.DefaultChangeCounts, resolved.ThreadCounts ?? BenchOptions.DefaultThreadCounts,
                               resolved.KeepServer, Log, cancellation, resolved.ChangeLinkTargets);
      await using var runner = new ScenarioRunner(context);
      scenarios = await runner.RunAsync();
    } finally {
      try {
        if (Directory.Exists(cacheDirectory)) {
          Directory.Delete(cacheDirectory, recursive: true);
        }
      } catch (Exception e) when (e is IOException or UnauthorizedAccessException) {
        Log($"Cannot delete the index cache {cacheDirectory}: {e.Message}");
      }
    }

    // 8. Results.
    // Note: The default label (machine and disk) is not part of the rerun command line (computed again).
    var arguments = RerunScript.ToArguments(resolved);
    var result = new BenchResult {
      ToolVersion = Version,
      StartedUtc = startedUtc,
      EndedUtc = DateTime.UtcNow,
      Label = label,
      MachineId = MachineIdentity.GetOrCreate(DataDirectory).ToString("D"),
      MachineLabel = resolved.MachineLabel,
      Machine = inventory.Machine,
      Disk = disk,
      Disks = inventory.Disks,
      Security = inventory.Security,
      Corpus = new CorpusInfo {
        Name = CommandLineParser.ParseCorpus(spec.Name),
        Path = corpus.RootPath,
        GeneratorVersion = corpus.Manifest.GeneratorVersion,
        FileCount = corpus.Manifest.FileCount,
        DirectoryCount = corpus.Manifest.DirectoryCount,
        TotalBytes = corpus.Manifest.TotalBytes,
        SearchableFileCount = corpus.Manifest.SearchableFileCount,
        SymlinkFileCount = corpus.Manifest.SymlinkFileCount,
        SymlinkDirectoryCount = corpus.Manifest.SymlinkDirectoryCount,
        Symlinks = symlinkNote ?? corpus.Manifest.Symlinks,
        GenerationSeconds = corpus.Generated ? corpus.Manifest.GenerationSeconds : null,
        FilesPerDirectoryHistogram = corpus.Manifest.FilesPerDirectoryHistogram,
      },
      Server = new ServerInfo {
        Source = server.Source,
        Path = server.Path,
        Version = serverVersion,
        ProtocolVersion = protocolVersion,
        Runtime = serverRuntime,
        Capabilities = capabilities.ToList(),
      },
      Run = new RunInfo {
        Scenarios = resolved.Scenarios!.Select(CommandLineParser.ScenarioName).ToList(),
        Repeat = resolved.Repeat!.Value,
        CacheMode = flush ? "cold" : "warm",
        CacheNote = cacheNote,
        ChangeCounts = context.ChangeCounts.ToList(),
        KeepServer = resolved.KeepServer,
        Arguments = arguments,
      },
      Scenarios = scenarios,
    };
    var resultsDir = Path.GetFullPath(resolved.ResultsDirectory ?? "results");
    Directory.CreateDirectory(resultsDir);
    var baseName = ResultBaseName(inventory.Machine.HostName, disk.MountPoint, result.Corpus.Name, startedUtc);
    var jsonPath = Path.Combine(resultsDir, baseName + ".json");
    File.WriteAllText(jsonPath, result.ToJson());
    var summary = MarkdownReport.Summary(result);
    File.WriteAllText(Path.Combine(resultsDir, baseName + ".md"), summary);
    output.WriteLine();
    output.WriteLine(summary);
    Log($"Results: {jsonPath}");

    // 9. Shareable report (no personal data).
    WriteShareableReport(result, Path.GetFullPath(resolved.ReportDirectory ?? resultsDir), [
      (corporaDir, "<corpora-dir>"),
      (cacheDirectory, "<cache-dir>"),
      (resultsDir, "<results-dir>"),
    ]);

    // 10. Rerun script.
    WriteRerunScript(resolved, arguments, baseName, resultsDir, label);
    return scenarios.Any(s => s.Error != null) ? 1 : 0;
  }

  public static string ResultBaseName(string machine, string mountPoint, string corpus, DateTime startedUtc) {
    var diskName = mountPoint.Trim('\\', '/', ':').Replace(":", "").Replace('\\', '-').Replace('/', '-');
    var raw = $"{machine}-{(diskName.Length == 0 ? "root" : diskName)}-{corpus}-{startedUtc:yyyyMMdd-HHmmss}";
    return new string(raw.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' or '.' ? ch : '-').ToArray());
  }

  private static string DefaultLabel(MachineInfo machine, DiskInfo disk) =>
    $"{machine.HostName} {disk.MountPoint.TrimEnd('\\')} {disk.FileSystem}{(disk.DevDrive == true ? " Dev Drive" : "")}";

  private DiskInfo SelectDisk(List<DiskInfo> disks, ref BenchOptions resolved) {
    if (disks.Count == 0) {
      throw new CommandLineException("No disk found.");
    }
    if (resolved.CorporaDirectory != null) {
      Directory.CreateDirectory(resolved.CorporaDirectory);
      var disk = InventoryCollector.FindDisk(disks, resolved.CorporaDirectory) ?? throw new CommandLineException($"No disk for \"{resolved.CorporaDirectory}\".");
      resolved = resolved with { Disk = disk.MountPoint.TrimEnd('\\') };
      return disk;
    }
    if (resolved.Disk != null) {
      var disk = InventoryCollector.FindDiskByName(disks, resolved.Disk) ?? throw new CommandLineException($"Unknown disk \"{resolved.Disk}\".");
      resolved = resolved with { Disk = disk.MountPoint.TrimEnd('\\') };
      return disk;
    }
    var current = InventoryCollector.FindDisk(disks, Environment.CurrentDirectory) ?? disks[0];
    var index = disks.IndexOf(current);
    if (Interactive) {
      index = prompt.Choose("Working disk (corpus and index cache):", disks.Select(d => (d.Summary, (string?)null)).ToList(), Math.Max(0, index));
    }
    resolved = resolved with { Disk = disks[index].MountPoint.TrimEnd('\\') };
    return disks[index];
  }

  /// <summary>
  /// The corpus. Symbolic links are included by default when the OS allows them (Windows: Developer
  /// Mode or elevation); otherwise the corpus without links is used, and <paramref name="symlinkNote"/>
  /// says why ("skipped (reason)").
  /// </summary>
  private CorpusSpec SelectCorpus(DiskInfo disk, string corporaDir, ref BenchOptions resolved, out string? symlinkNote) {
    symlinkNote = null;
    Directory.CreateDirectory(corporaDir);
    var symlinkProblem = CodeBaseGenerator.CanCreateSymlinks(corporaDir);
    var symlinks = resolved.Symlinks ?? symlinkProblem == null;
    if (resolved.Corpus == null && Interactive) {
      var items = CorpusCatalog.MenuNames.Select(name => {
        var spec = CorpusCatalog.Spec(name, symlinks && symlinkProblem == null);
        var exists = CorpusCache.TryGet(corporaDir, spec) != null;
        var estimate = CorpusCatalog.Estimate(spec, exists);
        var text = $"{name,-5} {spec.FileCount,10:n0} files, ~{DiskInfo.FormatBytes(estimate.CorpusBytes)} of files " +
                   $"+ ~{DiskInfo.FormatBytes(estimate.CacheBytes)} of index cache{(exists ? " (corpus present: cache only)" : "")}";
        return (text, estimate.Fits(disk.FreeBytes) ? null : $"{DiskInfo.FormatBytes(estimate.RequiredFreeBytes)} needed, {DiskInfo.FormatBytes(disk.FreeBytes)} free");
      }).ToList();
      var defaultIndex = Math.Max(0, items.FindIndex(i => i.Item2 == null));
      var choice = prompt.Choose($"Corpus (on {disk.MountPoint}, {DiskInfo.FormatBytes(disk.FreeBytes)} free):", items, defaultIndex);
      resolved = resolved with { Corpus = CorpusCatalog.MenuNames[choice] };
      if (resolved.Symlinks == null && symlinkProblem == null) {
        symlinks = prompt.YesNo("Include symbolic links in the corpus (file and directory links)?", true);
      }
    }
    // Note: An explicit "no" is part of the rerun command line; "yes" is the default.
    resolved = resolved with { Corpus = resolved.Corpus ?? BenchOptions.DefaultCorpus, Symlinks = symlinks ? null : false };
    if (symlinks && symlinkProblem != null) {
      symlinkNote = $"skipped ({symlinkProblem})";
      Log($"Symbolic links skipped: {symlinkProblem}");
      symlinks = false;
    }
    return CorpusCatalog.Spec(resolved.Corpus!, symlinks);
  }
  private async Task<ServerCandidate> SelectServerAsync(BenchOptions resolved, CancellationToken cancellation) {
    var locator = new ServerLocator(DataDirectory, Log);
    if (resolved.Server != null || !Interactive) {
      return await locator.ResolveAsync(resolved.Server ?? "auto", cancellation);
    }
    var local = locator.FindLocal();
    var items = local.Select(c => (c.Description, (string?)null)).ToList();
    items.Add(("latest release on GitHub (download)", null));
    items.Add(("another release on GitHub (enter the tag)", null));
    items.Add(("enter a path", null));
    var choice = prompt.Choose("Server:", items, 0);
    if (choice < local.Count) {
      return local[choice];
    }
    if (choice == local.Count) {
      return await locator.ResolveAsync("release:latest", cancellation);
    }
    if (choice == local.Count + 1) {
      var releases = await ServerLocator.GetReleasesAsync(null, cancellation);
      foreach (var release in releases) {
        output.WriteLine($"  {release.Tag}{(release.Prerelease ? " (prerelease)" : "")}: " +
                         (ReleaseAssets.Select(release.Assets, ReleaseAssets.CurrentRid) is { } s ? s.Package.Name : $"no server for {ReleaseAssets.CurrentRid}"));
      }
      return await locator.ResolveAsync("release:" + prompt.Ask("Release tag", releases.FirstOrDefault()?.Tag ?? "latest"), cancellation);
    }
    return await locator.ResolveAsync(prompt.Ask("Path of the server executable", ServerLocator.ExecutableName), cancellation);
  }

  private BenchOptions SelectScenarios(BenchOptions resolved) {
    var scenarios = resolved.Scenarios;
    var repeat = resolved.Repeat;
    if (Interactive) {
      if (scenarios == null) {
        output.WriteLine();
        output.WriteLine("Scenarios: 1 cold, 2 save, 3 restart, 4 live, 5 offline, 6 search, 7 rawfs, 8 threads");
        var text = prompt.Ask("Scenarios (names or numbers, comma separated)", string.Join(",", BenchOptions.DefaultScenarios.Select(CommandLineParser.ScenarioName)));
        scenarios = CommandLineParser.ParseScenarios(text);
      }
      if (repeat == null) {
        while (true) {
          if (int.TryParse(prompt.Ask("Repeats of each scenario", BenchOptions.DefaultRepeat.ToString()), out var r) && r > 0) {
            repeat = r;
            break;
          }
        }
      }
    }
    return resolved with {
      Scenarios = scenarios ?? BenchOptions.DefaultScenarios,
      Repeat = repeat ?? BenchOptions.DefaultRepeat,
      ChangeCounts = resolved.ChangeCounts ?? BenchOptions.DefaultChangeCounts,
      ThreadCounts = (scenarios ?? []).Contains(Scenario.Threads) ? resolved.ThreadCounts ?? BenchOptions.DefaultThreadCounts : resolved.ThreadCounts,
    };
  }

  /// <summary>Warm or cold file cache; cold needs admin/root and the agreement of the user.</summary>
  private (bool Flush, string? Note) SelectCacheMode(bool elevated, ref BenchOptions resolved) {
    var unavailable = CacheFlusher.Unavailable(elevated);
    var mode = resolved.Cache;
    if (mode == null && Interactive && unavailable == null) {
      mode = prompt.Choose("OS file cache:", [("warm (files cached by the OS, repeatable)", null), ("cold (flush the OS file cache before each measurement)", null)], 0) == 0
        ? CacheMode.Warm : CacheMode.Cold;
    }
    mode ??= CacheMode.Warm;
    resolved = resolved with { Cache = mode };
    if (mode == CacheMode.Warm) {
      return (false, unavailable != null ? "warm only: " + unavailable : null);
    }
    if (unavailable != null) {
      Log($"Cold file cache not possible ({unavailable}): warm only.");
      resolved = resolved with { Cache = CacheMode.Warm };
      return (false, "warm only: " + unavailable);
    }
    var agreed = resolved.FlushCache switch {
      YesNoAsk.Yes => true,
      YesNoAsk.No => false,
      _ => Interactive && prompt.YesNo(OperatingSystem.IsWindows()
        ? "Flush the OS file cache (purge the standby list of the memory manager) before each measurement?"
        : "Flush the OS file cache (drop_caches / purge) before each measurement?", true),
    };
    resolved = resolved with { FlushCache = agreed ? YesNoAsk.Yes : YesNoAsk.No };
    if (!agreed) {
      resolved = resolved with { Cache = CacheMode.Warm };
      return (false, "warm only: the flush of the OS file cache was declined");
    }
    return (true, null);
  }

  private void WriteRerunScript(BenchOptions resolved, List<string> arguments, string baseName, string resultsDir, string label) {
    var path = resolved.RerunScript;
    if (path == "none") {
      return;
    }
    var defaultPath = Path.Combine(resultsDir, RerunScript.DefaultFileName(baseName, OperatingSystem.IsWindows()));
    if (path == null) {
      if (!Interactive || !prompt.YesNo($"Save a script to rerun this exact test ({defaultPath})?", true)) {
        return;
      }
      path = defaultPath;
    }
    var benchPath = Environment.ProcessPath ?? "vschromium-bench";
    var written = RerunScript.Write(path, benchPath, arguments, $"Reruns the benchmark \"{label}\" (vschromium-bench {Version}, {DateTime.UtcNow:yyyy-MM-dd} UTC).");
    Log($"Rerun script: {written}");
  }
}
