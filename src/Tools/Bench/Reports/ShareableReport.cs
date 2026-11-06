// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Text;
using VsChromium.Bench.Inventory;
using VsChromium.Bench.Results;

namespace VsChromium.Bench.Reports;

/// <summary>Options of a shareable report.</summary>
public sealed record ReportOptions {
  /// <summary>The anonymous machine id (see <see cref="MachineIdentity"/>).</summary>
  public string MachineId { get; init; } = "";
  /// <summary>Free text label of the machine, included verbatim.</summary>
  public string? MachineLabel { get; init; }
  /// <summary>Omit the names of the security products and filter drivers.</summary>
  public bool RedactSecurity { get; init; }
  /// <summary>List the virtual and remote display adapters by name (default: "Remote session software: detected").</summary>
  public bool IncludeVirtualDisplays { get; init; }
}

/// <summary>
/// The shareable report of a run: a Markdown file meant to be committed (history of the results,
/// docs/benchmarks/results/), with the machine specs and every result but no identifying data: no
/// machine, user or domain name, no path (placeholders), no volume label, no serial number. The
/// machine is identified by an anonymous random id (<see cref="MachineIdentity"/>). The text is
/// scrubbed and verified before it is written (<see cref="Scrubber"/>).
/// </summary>
public static class ShareableReport {
  /// <summary>Options of the command line whose values are paths or free text: replaced by placeholders.</summary>
  private static readonly Dictionary<string, string> PathOptions = new() {
    ["--corpora-dir"] = "<corpora-dir>",
    ["--results-dir"] = "<results-dir>",
    ["--data-dir"] = "<data-dir>",
    ["--report-dir"] = "<report-dir>",
    ["--rerun-script"] = "<rerun-script>",
    ["--label"] = "<label>",
    ["--machine-label"] = "<machine-label>",
  };

  /// <summary>The file name: &lt;yyyy-MM-dd&gt;-&lt;machine id&gt;-&lt;file system[-devdrive]&gt;-&lt;corpus&gt;.md.</summary>
  public static string FileName(BenchResult result, ReportOptions options) {
    var fs = (result.Disk?.FileSystem ?? "disk").ToLowerInvariant() + (result.Disk?.DevDrive == true ? "-devdrive" : "");
    var raw = $"{result.StartedUtc:yyyy-MM-dd}-{MachineIdentity.Short(options.MachineId)}-{fs}-{result.Corpus.Name}";
    return new string(raw.Select(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '-').ToArray()).ToLowerInvariant() + ".md";
  }

  /// <summary>A mount point as shown in the reports: "D:" (Windows) or "/mnt/data".</summary>
  public static string Mount(string mountPoint) =>
    mountPoint.Length == 3 && mountPoint[1] == ':' ? mountPoint[..2] : mountPoint.Length > 1 ? mountPoint.TrimEnd('/', '\\') : mountPoint;

  /// <summary>Short description of the disk of the run, e.g. "D: (ReFS, Dev Drive, NVMe)".</summary>
  public static string DiskSummary(DiskInfo disk) {
    var parts = new List<string> { disk.FileSystem };
    if (disk.DevDrive == true) {
      parts.Add(disk.DevDriveTrusted == true ? "Dev Drive, trusted" : "Dev Drive");
    }
    parts.Add(disk.MediaType);
    return $"{Mount(disk.MountPoint)} ({string.Join(", ", parts)})";
  }

  private static string RoundedSize(long bytes) =>
    bytes >= 10L << 40 ? $"{bytes / (double)(1L << 40):n0} TB" :
    bytes >= 1L << 40 ? $"{bytes / (double)(1L << 40):n1} TB" :
    $"{Math.Round(bytes / (double)(1L << 30)):n0} GB";

  private static string Cell(string? text) => string.IsNullOrEmpty(text) ? "-" : text.Replace("|", "\\|").Replace("\n", " ");

  /// <summary>The non-interactive command line of the run, paths replaced by placeholders.</summary>
  public static string CommandLine(IReadOnlyList<string> arguments) {
    var result = new List<string> { "<bench>" };
    for (var i = 0; i < arguments.Count; i++) {
      var arg = arguments[i];
      result.Add(arg);
      if (i + 1 < arguments.Count && arg.StartsWith("--", StringComparison.Ordinal) && !arguments[i + 1].StartsWith("--", StringComparison.Ordinal)) {
        var value = arguments[++i];
        if (PathOptions.TryGetValue(arg, out var placeholder)) {
          value = value == "none" ? value : placeholder;
        } else if (arg == "--server" && (Path.IsPathRooted(value) || value.Contains('/') || value.Contains('\\'))) {
          value = "<server>";
        }
        result.Add(value.Contains(' ') ? $"\"{value}\"" : value);
      }
    }
    return string.Join(" ", result);
  }

  /// <summary>The text of the report (not scrubbed yet, see <see cref="Scrubber"/>).</summary>
  public static string Render(BenchResult r, ReportOptions options) {
    var text = new StringBuilder();
    var id = MachineIdentity.Short(options.MachineId);
    var disk = r.Disk;
    text.Append($"# vschromium-bench: {r.StartedUtc:yyyy-MM-dd} {id} {(disk != null ? DiskSummary(disk) : "")} {r.Corpus.Name}\n\n");
    text.Append("| | |\n|---|---|\n");
    text.Append($"| Date (UTC) | {r.StartedUtc:yyyy-MM-dd HH:mm} ({(r.EndedUtc - r.StartedUtc).TotalMinutes:n0} minutes) |\n");
    text.Append($"| Machine id | `{id}` (anonymous: random, stored in the data directory of the bench) |\n");
    if (!string.IsNullOrWhiteSpace(options.MachineLabel)) {
      text.Append($"| Machine label | {Cell(options.MachineLabel)} |\n");
    }
    text.Append($"| Bench | {Cell(r.ToolVersion)} |\n");
    var source = Path.IsPathRooted(r.Server.Source) || r.Server.Source is "repo" or "vscode" ? "local build" : r.Server.Source;
    text.Append($"| Server | {Cell(r.Server.Version)} ({Cell(source)}), protocol {r.Server.ProtocolVersion} |\n");
    text.Append($"| Server runtime | {Cell(r.Server.Runtime)} |\n");
    text.Append($"| Server bench features | {Cell(r.Server.Capabilities.Count > 0 ? string.Join(", ", r.Server.Capabilities) : "none")} |\n");
    text.Append($"| Test disk | {(disk != null ? DiskSummary(disk) : "-")} |\n");
    text.Append($"| File cache | {Cell(r.Run.CacheMode + (r.Run.CacheNote != null ? $" ({r.Run.CacheNote})" : ""))} |\n\n");

    var m = r.Machine;
    text.Append("## Machine\n\n| | |\n|---|---|\n");
    text.Append($"| OS | {Cell(m.OsDescription)} ({Cell(m.Architecture)}) |\n");
    text.Append($"| CPU | {Cell(m.CpuModel)} |\n");
    text.Append($"| Cores | {(m.PhysicalCores is { } cores ? $"{cores} physical, " : "")}{m.LogicalCores} logical processors |\n");
    var clocks = new List<string>();
    if (m.BaseClockMHz is { } baseClock) {
      clocks.Add($"base {baseClock / 1000.0:0.0#} GHz");
    }
    if (m.MaxClockMHz is { } maxClock) {
      clocks.Add($"max {maxClock / 1000.0:0.0#} GHz");
    }
    text.Append($"| Clock | {(clocks.Count > 0 ? string.Join(", ", clocks) : "-")} |\n");
    var memory = DiskInfo.FormatBytes(m.PhysicalMemoryBytes);
    if (m.MemoryType != null || m.MemorySpeedMTs != null) {
      memory += $" ({string.Join(" ", new[] { m.MemoryType, m.MemorySpeedMTs is { } mts ? $"{mts} MT/s" : null }.Where(s => s != null))})";
    }
    text.Append($"| RAM | {memory} |\n");
    var gpus = m.Gpus ?? [];
    var virtualGpus = gpus.Where(g => g.IsVirtual || InventoryCollector.IsVirtualDisplay(g.Name)).ToList();
    var listed = options.IncludeVirtualDisplays ? gpus : gpus.Except(virtualGpus).ToList();
    var gpuText = string.Join("; ", listed.Select(g => g.Name + (g.DriverVersion != null ? $" (driver {g.DriverVersion})" : "") +
                                                       (options.IncludeVirtualDisplays && virtualGpus.Contains(g) ? " (virtual)" : "")));
    text.Append($"| GPU | {Cell(gpuText)} |\n");
    if (virtualGpus.Count > 0 && !options.IncludeVirtualDisplays) {
      // Note: Virtual and remote display adapters reveal the remote access software in use: not named.
      text.Append("| Remote session software | detected |\n");
    }
    text.Append($"| Remote session | {(m.RemoteSession switch { true => "yes", false => "no", null => "-" })} |\n");
    text.Append($"| Power plan | {Cell(m.PowerPlan)} |\n");
    text.Append($"| Bench runtime | {Cell(m.BenchRuntime)}{(m.IsElevated ? ", elevated" : "")} |\n\n");

    text.Append("## Drives\n\n");
    text.Append("| Drive | File system | Dev Drive | Size | Free | Media | Model | Bus | Change journal |\n");
    text.Append("|---|---|---|---:|---:|---|---|---|---|\n");
    var disks = r.Disks is { Count: > 0 } ? r.Disks : disk != null ? [disk] : [];
    foreach (var d in disks.Where(d => d.MediaType is not ("Network" or "Removable"))) {
      var test = disk != null && d.MountPoint == disk.MountPoint ? " **(test)**" : "";
      var devDrive = d.DevDrive switch { true => d.DevDriveTrusted == true ? "yes, trusted" : "yes", false => "no", null => "-" };
      var journal = d.ChangeJournal == null ? "-" : d.ChangeJournal + (d.ChangeJournalMaxBytes is { } max ? $" (max {DiskInfo.FormatBytes(max)})" : "");
      // Note: The model is the product string of the device; on Linux, the device is a path (/dev/...): not shown.
      var model = d.Device != null && !d.Device.StartsWith('/') ? d.Device : null;
      text.Append($"| {Cell(Mount(d.MountPoint))}{test} | {Cell(d.FileSystem)} | {devDrive} | {RoundedSize(d.TotalBytes)} | {RoundedSize(d.FreeBytes)} | " +
                  $"{Cell(d.MediaType)} | {Cell(model)} | {Cell(d.BusType)} | {Cell(journal)} |\n");
    }
    text.Append('\n');

    var s = r.Security;
    text.Append("## Security software\n\n");
    if (options.RedactSecurity) {
      text.Append("Not included (--redact-security).\n\n");
    } else {
      text.Append("| | |\n|---|---|\n");
      text.Append($"| Antivirus | {Cell(s.Antivirus.Count > 0 ? string.Join(", ", s.Antivirus.Select(a => $"{a.Name} ({a.State})")) : s.AntivirusNote)} |\n");
      text.Append($"| Defender | {Cell(s.DefenderRealTimeProtection is { } rt ? $"{s.DefenderMode}, real-time protection {(rt ? "on" : "off")}" : s.DefenderNote)} |\n");
      text.Append($"| Filter drivers | {Cell(s.FilterDrivers is { } f ? string.Join(", ", f) : s.FilterDriversNote)} |\n\n");
    }

    var c = r.Corpus;
    text.Append("## Corpus\n\n| | |\n|---|---|\n");
    text.Append($"| Corpus | {Cell(c.Name)} (generator version {c.GeneratorVersion}) |\n");
    text.Append($"| Files | {c.FileCount:n0} ({c.SearchableFileCount:n0} searchable) |\n");
    text.Append($"| Directories | {c.DirectoryCount:n0} |\n");
    text.Append($"| Size | {DiskInfo.FormatBytes(c.TotalBytes)} ({c.TotalBytes:n0} bytes) |\n");
    text.Append($"| Symbolic links | {Cell(c.Symlinks)}{(c.SymlinkFileCount + c.SymlinkDirectoryCount > 0 ? $": {c.SymlinkFileCount} file links, {c.SymlinkDirectoryCount} directory links" : "")} |\n");
    if (c.GenerationSeconds is { } generation) {
      text.Append($"| Generation | {generation:n1} s |\n");
    }
    text.Append('\n');
    if (c.FilesPerDirectoryHistogram is { Count: > 0 } histogram) {
      text.Append("| Files per directory | Directories | % | Files | % |\n|---|---:|---:|---:|---:|\n");
      foreach (var b in histogram) {
        text.Append($"| {b.Bucket} | {b.DirectoryCount:n0} | {b.DirectoryPercent:n1} | {b.FileCount:n0} | {b.FilePercent:n1} |\n");
      }
      text.Append('\n');
    }

    text.Append("## Options\n\n| | |\n|---|---|\n");
    text.Append($"| Scenarios | {Cell(string.Join(", ", r.Run.Scenarios))} |\n");
    text.Append($"| Repeats | {r.Run.Repeat} |\n");
    text.Append($"| File changes | {Cell(string.Join(", ", r.Run.ChangeCounts.Select(n => n.ToString("n0", CultureInfo.InvariantCulture))))} |\n");
    text.Append($"| Keep server | {(r.Run.KeepServer ? "yes" : "no")} |\n\n");

    text.Append("## Results\n\n");
    text.Append(MarkdownReport.ScenarioTables(r, "###"));

    text.Append("## Command line\n\n```\n").Append(CommandLine(r.Run.Arguments)).Append("\n```\n");
    return text.ToString();
  }

  /// <summary>
  /// The sensitive terms of a result: its recorded host name and paths (corpus, corpora directory,
  /// server), in addition to those of the current machine.
  /// </summary>
  public static List<SensitiveTerm> TermsOf(BenchResult r, IEnumerable<(string Path, string Placeholder)> extraPaths) {
    var paths = new List<(string, string)>(extraPaths);
    if (r.Corpus.Path.Length > 0) {
      paths.Add((r.Corpus.Path, "<corpus-root>"));
      if (Path.GetDirectoryName(r.Corpus.Path) is { Length: > 0 } corpora) {
        paths.Add((corpora, "<corpora-dir>"));
      }
    }
    if (Path.IsPathRooted(r.Server.Path)) {
      paths.Add((r.Server.Path, "<server>"));
      if (Path.GetDirectoryName(r.Server.Path) is { Length: > 0 } serverDir) {
        paths.Add((serverDir, "<server-dir>"));
      }
    }
    var terms = Scrubber.CurrentMachineTerms(paths);
    if (r.Machine.HostName.Length > 0) {
      terms.Add(new SensitiveTerm(r.Machine.HostName, "<machine>", IsPath: false));
    }
    return terms;
  }

  /// <summary>Renders, scrubs, verifies and writes the report; returns its path. Throws <see cref="SensitiveDataException"/>.</summary>
  public static string Write(BenchResult r, ReportOptions options, string directory, IEnumerable<(string Path, string Placeholder)> extraPaths) {
    var text = Scrubber.ScrubAndVerify(Render(r, options), TermsOf(r, extraPaths));
    Directory.CreateDirectory(directory);
    var path = Path.Combine(directory, FileName(r, options));
    // Another run of the same day, machine, disk and corpus: a numbered file (the report of the same
    // run is replaced).
    var runDate = $"| Date (UTC) | {r.StartedUtc:yyyy-MM-dd HH:mm} ";
    for (var i = 2; File.Exists(path) && !File.ReadAllText(path).Contains(runDate, StringComparison.Ordinal); i++) {
      path = Path.Combine(directory, Path.GetFileNameWithoutExtension(FileName(r, options)) + $"-{i}.md");
    }
    File.WriteAllText(path, text);
    return path;
  }
}
