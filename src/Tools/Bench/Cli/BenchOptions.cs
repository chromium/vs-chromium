// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

namespace VsChromium.Bench.Cli;

/// <summary>The commands of the benchmark app.</summary>
public enum BenchCommand { Run, Compare, Report, Cleanup, Inventory, Help }

/// <summary>Answer to a question asked when the bench runs elevated (admin/root): yes, no, or ask.</summary>
public enum YesNoAsk { Ask, Yes, No }

/// <summary>Whether the OS file cache is flushed before the measured phases.</summary>
public enum CacheMode { Warm, Cold }

/// <summary>The scenarios of the benchmark (see the design document, "Scenarios").</summary>
public enum Scenario { Cold, Save, Restart, Live, Offline, Search, RawFs, Threads }

/// <summary>
/// All the options of the benchmark app. Every interactive choice has an option, so that a run can be
/// repeated exactly (see <see cref="RerunScript"/>). Null means "not specified": asked interactively,
/// or the default in non-interactive mode.
/// </summary>
public sealed record BenchOptions {
  public BenchCommand Command { get; init; } = BenchCommand.Run;
  public bool NonInteractive { get; init; }

  /// <summary>The working disk: a drive ("D:") or a mount point ("/mnt/data").</summary>
  public string? Disk { get; init; }

  /// <summary>The directory of the corpora (default: &lt;disk&gt;/vschromium-corpora).</summary>
  public string? CorporaDirectory { get; init; }

  /// <summary>The corpus: 400k, 1M, 2M (or small, medium for quick tests).</summary>
  public string? Corpus { get; init; }

  /// <summary>Generate the corpus with symbolic links (a separate corpus directory).</summary>
  public bool? Symlinks { get; init; }

  /// <summary>
  /// The server: a path, "repo", "vscode", "release:latest", "release:&lt;tag&gt;", or "auto" (the first one
  /// found).
  /// </summary>
  public string? Server { get; init; }

  public IReadOnlyList<Scenario>? Scenarios { get; init; }
  public int? Repeat { get; init; }
  public CacheMode? Cache { get; init; }

  /// <summary>Flush the OS file cache (cold runs, elevated only).</summary>
  public YesNoAsk FlushCache { get; init; } = YesNoAsk.Ask;

  /// <summary>List the file system filter drivers (elevated only, Windows).</summary>
  public YesNoAsk ListFilters { get; init; } = YesNoAsk.Ask;

  /// <summary>Keep one server process across the "live" and "search" scenarios (default: true).</summary>
  public bool KeepServer { get; init; } = true;

  /// <summary>The numbers of files modified by the "live" and "offline" scenarios.</summary>
  public IReadOnlyList<int>? ChangeCounts { get; init; }

  /// <summary>The thread counts of the "threads" scenario (needs a server supporting "--threads").</summary>
  public IReadOnlyList<int>? ThreadCounts { get; init; }

  /// <summary>The directory of the results (default: ./results).</summary>
  public string? ResultsDirectory { get; init; }

  /// <summary>A label of the run (column header of "compare"), default: machine and disk.</summary>
  public string? Label { get; init; }

  /// <summary>Path of the rerun script to write, "none" to not write it, null to ask (or the default name).</summary>
  public string? RerunScript { get; init; }

  /// <summary>Data directory of the bench (downloaded servers, index caches).</summary>
  public string? DataDirectory { get; init; }

  /// <summary>
  /// Free text label of the machine in the shareable reports (included verbatim: no personal data).
  /// </summary>
  public string? MachineLabel { get; init; }

  /// <summary>The directory of the shareable reports (default: the results directory).</summary>
  public string? ReportDirectory { get; init; }

  /// <summary>Omit the security products and filter drivers from the shareable reports.</summary>
  public bool RedactSecurity { get; init; }

  /// <summary>"compare" and "report": the result files.</summary>
  public IReadOnlyList<string> Files { get; init; } = [];

  /// <summary>"compare": the output Markdown file (default: printed only).</summary>
  public string? Output { get; init; }

  /// <summary>"cleanup": what to delete (corpora, caches, servers, results, all).</summary>
  public IReadOnlyList<string>? CleanupTargets { get; init; }

  /// <summary>Answer yes to the confirmations (e.g. of "cleanup").</summary>
  public bool Yes { get; init; }

  /// <summary>"inventory": print JSON instead of text.</summary>
  public bool Json { get; init; }

  public static readonly IReadOnlyList<Scenario> DefaultScenarios =
    [Scenario.Cold, Scenario.Save, Scenario.Restart, Scenario.Live, Scenario.Offline, Scenario.Search, Scenario.RawFs];

  public static readonly IReadOnlyList<int> DefaultChangeCounts = [1, 1_000, 10_000];
  public static readonly IReadOnlyList<int> DefaultThreadCounts = [1, 4, 8, 0];
  public const int DefaultRepeat = 3;
  public const string DefaultCorpus = "400k";
}
