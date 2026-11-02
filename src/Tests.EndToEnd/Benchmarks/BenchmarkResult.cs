// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace VsChromium.Tests.EndToEnd.Benchmarks;

public sealed record BenchmarkEnvironment(string MachineName, string OS, string Architecture, int ProcessorCount,
                                          string ServerPath, string ServerRuntime, string ServerGitCommit);

public sealed record CorpusInfo(string Name, string RootPath, int FileCount, long TotalBytes, int SearchableFileCount);

/// <summary>Resource usage of a scenario, measured from outside (sampler) and inside (server statistics).</summary>
public sealed record ScenarioResult(string Name, ProcessUsage External, JsonObject? ServerStatisticsAfter);

public sealed record QueryResult(
  string Name,
  string Kind,
  int Runs,
  double ClientP50Ms,
  double ClientP95Ms,
  double ClientMaxMs,
  double ServerAvgMs,
  double ServerCpuMsPerRun,
  int ResponseBytes,
  long HitCount,
  long? ExpectedHitCount,
  bool Correct);

public sealed record BenchmarkResult(
  DateTime StartedUtc,
  BenchmarkEnvironment Environment,
  CorpusInfo Corpus,
  List<ScenarioResult> Scenarios,
  List<QueryResult> Queries) {

  private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

  public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

  public static BenchmarkResult FromJson(string json) => JsonSerializer.Deserialize<BenchmarkResult>(json)!;

  public static BenchmarkEnvironment CaptureEnvironment(string serverPath, string serverRuntime) =>
    new(System.Environment.MachineName, RuntimeInformation.OSDescription, RuntimeInformation.OSArchitecture.ToString(),
        System.Environment.ProcessorCount, serverPath, serverRuntime, GitCommit());

  private static string GitCommit() {
    try {
      var psi = new System.Diagnostics.ProcessStartInfo("git", "rev-parse --short HEAD") {
        WorkingDirectory = TestSettings.RepositoryRoot, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
      };
      using var p = System.Diagnostics.Process.Start(psi)!;
      var output = p.StandardOutput.ReadToEnd().Trim();
      p.WaitForExit();
      return p.ExitCode == 0 ? output : "unknown";
    } catch (System.ComponentModel.Win32Exception) {
      return "unknown";
    }
  }

  // --- Reports ----------------------------------------------------------------------------------

  public string ToMarkdown() {
    var sb = new StringBuilder();
    sb.AppendLine($"# Server benchmark: {Corpus.Name} corpus");
    sb.AppendLine();
    sb.AppendLine($"- Date: {StartedUtc:u}");
    sb.AppendLine($"- Machine: {Environment.MachineName}, {Environment.OS} {Environment.Architecture}, {Environment.ProcessorCount} logical processors");
    sb.AppendLine($"- Server: `{Environment.ServerPath}` ({Environment.ServerRuntime}), commit {Environment.ServerGitCommit}");
    sb.AppendLine($"- Corpus: {Corpus.FileCount:n0} files, {Mb(Corpus.TotalBytes)} ({Corpus.SearchableFileCount:n0} searchable), `{Corpus.RootPath}`");
    sb.AppendLine();
    sb.AppendLine("## Scenarios");
    sb.AppendLine();
    sb.AppendLine("| Scenario | Wall time | CPU time | Avg cores | Peak working set | Peak private bytes | GC heap | Native allocated |");
    sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
    foreach (var s in Scenarios) {
      sb.AppendLine($"| {s.Name} | {Ms(s.External.WallMs)} | {Ms(s.External.CpuMs)} | {s.External.AverageCores:n2} | " +
                    $"{Mb(s.External.PeakWorkingSetBytes)} | {Mb(s.External.PeakPrivateBytes)} | " +
                    $"{Mb(Long(s.ServerStatisticsAfter, "gcHeapBytes"))} | {Mb(Long(s.ServerStatisticsAfter, "nativeAllocatedBytes"))} |");
    }
    sb.AppendLine();
    sb.AppendLine("## Queries");
    sb.AppendLine();
    sb.AppendLine("| Query | Kind | Runs | p50 | p95 | Max | Server avg | Server CPU/run | Response | Hits | Correct |");
    sb.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---|");
    foreach (var q in Queries) {
      var expected = q.ExpectedHitCount is { } e ? $" / {e:n0}" : "";
      sb.AppendLine($"| `{q.Name}` | {q.Kind} | {q.Runs} | {Ms(q.ClientP50Ms)} | {Ms(q.ClientP95Ms)} | {Ms(q.ClientMaxMs)} | " +
                    $"{Ms(q.ServerAvgMs)} | {Ms(q.ServerCpuMsPerRun)} | {Kb(q.ResponseBytes)} | {q.HitCount:n0}{expected} | {(q.Correct ? "yes" : "**NO**")} |");
    }
    return sb.ToString();
  }

  /// <summary>Markdown comparison of a baseline and a candidate result (same corpus).</summary>
  public static string Compare(BenchmarkResult baseline, BenchmarkResult candidate) {
    var sb = new StringBuilder();
    sb.AppendLine($"# Benchmark comparison: {candidate.Corpus.Name} corpus");
    sb.AppendLine();
    sb.AppendLine($"- Baseline: commit {baseline.Environment.ServerGitCommit}, {baseline.Environment.ServerRuntime}, {baseline.StartedUtc:u}");
    sb.AppendLine($"- Candidate: commit {candidate.Environment.ServerGitCommit}, {candidate.Environment.ServerRuntime}, {candidate.StartedUtc:u}");
    sb.AppendLine();
    sb.AppendLine("| Scenario | Wall time | CPU time | Peak working set | Peak private bytes |");
    sb.AppendLine("|---|---:|---:|---:|---:|");
    foreach (var c in candidate.Scenarios) {
      var b = baseline.Scenarios.FirstOrDefault(x => x.Name == c.Name);
      if (b == null) {
        continue;
      }
      sb.AppendLine($"| {c.Name} | {Delta(b.External.WallMs, c.External.WallMs, Ms)} | {Delta(b.External.CpuMs, c.External.CpuMs, Ms)} | " +
                    $"{Delta(b.External.PeakWorkingSetBytes, c.External.PeakWorkingSetBytes, Mb)} | " +
                    $"{Delta(b.External.PeakPrivateBytes, c.External.PeakPrivateBytes, Mb)} |");
    }
    sb.AppendLine();
    sb.AppendLine("| Query | p50 | p95 | Server CPU/run | Hits (baseline → candidate) |");
    sb.AppendLine("|---|---:|---:|---:|---|");
    foreach (var c in candidate.Queries) {
      var b = baseline.Queries.FirstOrDefault(x => x.Name == c.Name && x.Kind == c.Kind);
      if (b == null) {
        continue;
      }
      var hits = b.HitCount == c.HitCount ? $"{c.HitCount:n0}" : $"**{b.HitCount:n0} → {c.HitCount:n0}**";
      sb.AppendLine($"| `{c.Name}` ({c.Kind}) | {Delta(b.ClientP50Ms, c.ClientP50Ms, Ms)} | {Delta(b.ClientP95Ms, c.ClientP95Ms, Ms)} | " +
                    $"{Delta(b.ServerCpuMsPerRun, c.ServerCpuMsPerRun, Ms)} | {hits} |");
    }
    return sb.ToString();
  }

  private static string Delta(double baseline, double candidate, Func<double, string> format) {
    if (baseline <= 0) {
      return format(candidate);
    }
    var percent = (candidate - baseline) / baseline * 100;
    return $"{format(baseline)} → {format(candidate)} ({percent.ToString("+0;-0;0", CultureInfo.InvariantCulture)}%)";
  }

  private static long Long(JsonObject? obj, string name) => obj?[name]?.GetValue<long>() ?? -1;
  private static string Ms(double ms) => ms < 0 ? "n/a" : ms >= 10_000 ? $"{ms / 1000:n1} s" : $"{ms:n1} ms";
  private static string Mb(double bytes) => bytes < 0 ? "n/a" : $"{bytes / (1024 * 1024):n1} MB";
  private static string Kb(double bytes) => $"{bytes / 1024:n1} KB";
}
