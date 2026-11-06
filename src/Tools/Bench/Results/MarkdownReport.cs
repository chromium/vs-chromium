// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Text;
using VsChromium.Bench.Inventory;

namespace VsChromium.Bench.Results;

/// <summary>Markdown views of results: the summary of a run, and the side-by-side comparison of several runs.</summary>
public static class MarkdownReport {
  /// <summary>Formats a value with its unit (medians in tables).</summary>
  public static string Format(double value, string unit) => unit switch {
    "bytes" => DiskInfo.FormatBytes((long)value),
    "s" => value >= 100 ? $"{value.ToString("n0", CultureInfo.InvariantCulture)} s" : value >= 1 ? $"{value.ToString("n2", CultureInfo.InvariantCulture)} s" : $"{(value * 1000).ToString("n0", CultureInfo.InvariantCulture)} ms",
    "ms" => value >= 100 ? $"{value.ToString("n0", CultureInfo.InvariantCulture)} ms" : $"{value.ToString("n1", CultureInfo.InvariantCulture)} ms",
    "us" => $"{value.ToString("n1", CultureInfo.InvariantCulture)} µs",
    "count" => value.ToString("n0", CultureInfo.InvariantCulture),
    "files/s" => $"{value.ToString("n0", CultureInfo.InvariantCulture)}/s",
    "MB/s" => $"{value.ToString("n0", CultureInfo.InvariantCulture)} MB/s",
    _ => value.ToString("g4", CultureInfo.InvariantCulture) + (unit.Length > 0 ? " " + unit : ""),
  };

  private static string Escape(string text) => text.Replace("|", "\\|").Replace("\n", " ");

  /// <summary>Header rows describing the machine and the run (shared by the summary and the comparison).</summary>
  public static List<(string Name, Func<BenchResult, string> Value)> DescriptionRows { get; } = [
    ("Machine", r => r.Machine.HostName),
    ("OS", r => r.Machine.OsDescription),
    ("CPU", r => $"{r.Machine.CpuModel} ({r.Machine.LogicalCores} threads)"),
    ("RAM", r => DiskInfo.FormatBytes(r.Machine.PhysicalMemoryBytes)),
    ("Power plan", r => r.Machine.PowerPlan ?? "-"),
    ("Disk", r => r.Disk == null ? "?" : $"{r.Disk.MountPoint} {r.Disk.FileSystem}{(r.Disk.DevDrive == true ? " Dev Drive" + (r.Disk.DevDriveTrusted == true ? " (trusted)" : "") : "")}"),
    ("Media", r => r.Disk == null ? "?" : r.Disk.MediaType + (r.Disk.BusType != null ? $" ({r.Disk.BusType})" : "")),
    ("Change journal", r => r.Disk?.ChangeJournal ?? "-"),
    ("Antivirus", r => r.Security.Antivirus.Count > 0 ? string.Join(", ", r.Security.Antivirus.Select(a => $"{a.Name} ({a.State})")) : r.Security.AntivirusNote ?? "-"),
    ("Defender", r => r.Security.DefenderRealTimeProtection is { } rt ? $"{r.Security.DefenderMode}, real-time {(rt ? "on" : "off")}" : r.Security.DefenderNote ?? "-"),
    ("Filter drivers", r => r.Security.FilterDrivers is { } f ? string.Join(", ", f) : r.Security.FilterDriversNote ?? "-"),
    ("Corpus", r => $"{r.Corpus.Name}: {r.Corpus.FileCount:n0} files, {r.Corpus.DirectoryCount:n0} dirs, {DiskInfo.FormatBytes(r.Corpus.TotalBytes)}" +
                    (r.Corpus.Symlinks != "none" ? $", symlinks {r.Corpus.Symlinks}" : "")),
    ("Server", r => $"{r.Server.Version} ({(Path.IsPathRooted(r.Server.Source) ? "local build" : r.Server.Source)})" +
                    (r.Server.Capabilities.Count > 0 ? $", bench features: {string.Join(" ", r.Server.Capabilities)}" : "")),
    ("File cache", r => r.Run.CacheMode + (r.Run.CacheNote != null ? $" ({r.Run.CacheNote})" : "")),
    ("Repeats", r => r.Run.Repeat.ToString(CultureInfo.InvariantCulture)),
  ];

  /// <summary>The Markdown summary of a run: description, then one table per scenario (median, min, max).</summary>
  public static string Summary(BenchResult result) {
    var text = new StringBuilder();
    text.Append("# vschromium-bench: ").Append(Escape(result.Label)).Append("\n\n");
    text.Append($"{result.StartedUtc:yyyy-MM-dd HH:mm} UTC, {(result.EndedUtc - result.StartedUtc).TotalMinutes:n1} minutes, bench {result.ToolVersion}\n\n");
    text.Append("| | |\n|---|---|\n");
    foreach (var (name, value) in DescriptionRows) {
      text.Append($"| {name} | {Escape(value(result))} |\n");
    }
    if (result.Corpus.GenerationSeconds is { } generation) {
      text.Append($"| Corpus generation | {generation:n1} s |\n");
    }
    text.Append('\n');
    foreach (var scenario in result.Scenarios) {
      text.Append("## ").Append(scenario.Name).Append("\n\n");
      if (scenario.Error != null) {
        text.Append("**Error:** ").Append(Escape(scenario.Error)).Append("\n\n");
      }
      if (scenario.Skipped) {
        text.Append("Skipped. ");
      }
      if (scenario.Metrics.Count > 0) {
        text.Append("| Metric | Median | Min | Max |\n|---|---:|---:|---:|\n");
        foreach (var m in scenario.Metrics) {
          text.Append($"| {Escape(m.Name)} | {Format(m.Median, m.Unit)} | {Format(m.Min, m.Unit)} | {Format(m.Max, m.Unit)} |\n");
        }
        text.Append('\n');
      }
      foreach (var (key, value) in scenario.Info) {
        text.Append($"- {Escape(key)}: {Escape(value)}\n");
      }
      foreach (var note in scenario.Notes) {
        text.Append($"- {Escape(note)}\n");
      }
      if (scenario.Info.Count > 0 || scenario.Notes.Count > 0) {
        text.Append('\n');
      }
    }
    return text.ToString();
  }

  /// <summary>The column label of a result in a comparison: its label, made unique.</summary>
  public static List<string> ColumnLabels(IReadOnlyList<BenchResult> results) {
    var labels = new List<string>();
    foreach (var r in results) {
      var label = r.Label.Length > 0 ? r.Label : $"{r.Machine.HostName} {r.Disk?.MountPoint}";
      var unique = label;
      for (var i = 2; labels.Contains(unique); i++) {
        unique = $"{label} ({i})";
      }
      labels.Add(unique);
    }
    return labels;
  }

  /// <summary>
  /// The side-by-side comparison of several results: the description rows, then one row per metric
  /// (median), one column per result, in the order of first appearance. The last column is the
  /// ratio of the last result to the first one when there are exactly two.
  /// </summary>
  public static string Compare(IReadOnlyList<BenchResult> results) {
    var labels = ColumnLabels(results);
    var two = results.Count == 2;
    var text = new StringBuilder();
    text.Append("| | ").Append(string.Join(" | ", labels.Select(Escape))).Append(two ? " | ratio |" : " |").Append('\n');
    text.Append("|---|").Append(string.Concat(labels.Select(_ => "---:|"))).Append(two ? "---:|" : "").Append('\n');
    foreach (var (name, value) in DescriptionRows) {
      text.Append($"| {name} | ").Append(string.Join(" | ", results.Select(r => Escape(value(r))))).Append(two ? " | |" : " |").Append('\n');
    }
    var keys = new List<(string Scenario, string Metric, string Unit)>();
    foreach (var r in results) {
      foreach (var s in r.Scenarios) {
        foreach (var m in s.Metrics) {
          if (!keys.Any(k => k.Scenario == s.Name && k.Metric == m.Name)) {
            keys.Add((s.Name, m.Name, m.Unit));
          }
        }
      }
    }
    foreach (var (scenario, metric, unit) in keys) {
      var values = results.Select(r => r.Scenarios.FirstOrDefault(s => s.Name == scenario)?.Metrics.FirstOrDefault(m => m.Name == metric)?.Median).ToList();
      text.Append($"| {Escape(scenario)}: {Escape(metric)} | ").Append(string.Join(" | ", values.Select(v => v is { } x ? Format(x, unit) : "-")));
      if (two) {
        text.Append(" | ").Append(values[0] is { } a && values[1] is { } b && a != 0 ? $"{(b / a).ToString("n2", CultureInfo.InvariantCulture)}x" : "-");
      }
      text.Append(" |\n");
    }
    return text.ToString();
  }
}
