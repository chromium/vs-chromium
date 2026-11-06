// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Globalization;

namespace VsChromium.Bench.Cli;

/// <summary>An error in the command line (message for the user).</summary>
public sealed class CommandLineException(string message) : Exception(message);

/// <summary>
/// Parses the command line of the benchmark app: <c>vschromium-bench [command] [--option value | --option=value | --flag]...</c>.
/// The command is optional (default: run). See <see cref="Usage"/>.
/// </summary>
public static class CommandLineParser {
  public const string Usage = """
    Usage: vschromium-bench [run|compare|report|cleanup|inventory|help] [options]

    Commands:
      run (default)   Run the benchmark (interactive unless --non-interactive).
      compare <files> Side-by-side Markdown table of result files (.json).
      report <files>  Shareable report (Markdown, no personal data) of result files (.json).
      cleanup         Delete generated corpora, index caches, downloaded servers.
      inventory       Show the machine and disk inventory.

    Options of "run":
      --non-interactive          Never ask: use the options and the defaults.
      --disk <drive|mount>       Working disk, e.g. D: or /mnt/data (default: the disk of the current directory).
      --corpora-dir <dir>        Directory of the corpora (default: <disk>/vschromium-corpora).
      --corpus <400k|1M|2M>      Corpus size (default: 400k; small and medium for quick tests).
      --symlinks <yes|no>        Corpus with symbolic links (default: yes when the OS allows them).
      --no-symlinks              Same as --symlinks no.
      --change-link-targets      The live and offline changes also modify files seen through symbolic links.
      --server <source>          auto (default), repo, vscode, release:latest, release:<tag>, or a path.
      --scenarios <list>         cold,save,restart,live,offline,search,rawfs,threads (default: all but threads).
      --repeat <n>               Repeats of each scenario (default: 3).
      --cache <warm|cold>        OS file cache state (default: warm; cold needs admin/root).
      --flush-cache <yes|no>     Pre-answer the question of flushing the OS file cache (elevated only).
      --list-filters <yes|no>    Pre-answer the question of listing the filter drivers (elevated, Windows).
      --no-keep-server           A new server process for each live/search scenario.
      --changes <list>           Files modified by live/offline (default: 1,1000,10000).
      --threads <list>           Thread counts of the threads scenario (default: 1,4,8,0 = all).
      --results-dir <dir>        Directory of the results (default: ./results).
      --label <text>             Label of the run (column header of compare).
      --rerun-script <path|none> Rerun script to write (default: asked, or rerun-<name>.cmd/.sh).
      --data-dir <dir>           Data directory of the bench (downloaded servers, caches).
      --report-dir <dir>         Directory of the shareable report (default: the results directory).
      --machine-label <text>     Label of the machine in the shareable report (included verbatim).
      --redact-security          Omit security products and filter drivers from the shareable report.
      --include-virtual-displays List the virtual and remote display adapters by name in the shareable report.

    Options of "compare":   <files...> [--output <file.md>]
    Options of "report":    <files...> [--report-dir <dir>] [--machine-label <text>] [--redact-security] [--include-virtual-displays]
    Options of "cleanup":   [--corpora] [--caches] [--servers] [--results] [--all] [--disk <d>] [--yes]
    Options of "inventory": [--json] [--list-filters <yes|no>]
    """;

  public static BenchOptions Parse(IReadOnlyList<string> args) {
    var options = new BenchOptions();
    var index = 0;
    if (args.Count > 0 && !args[0].StartsWith('-')) {
      options = options with { Command = ParseCommand(args[0]) };
      index = 1;
    }
    var files = new List<string>();
    var cleanup = new List<string>();
    for (; index < args.Count; index++) {
      var arg = args[index];
      if (!arg.StartsWith("--", StringComparison.Ordinal)) {
        if (arg is "-h" or "-?" or "/?") {
          options = options with { Command = BenchCommand.Help };
          continue;
        }
        if (options.Command is BenchCommand.Compare or BenchCommand.Report) {
          files.Add(arg);
          continue;
        }
        throw new CommandLineException($"Unexpected argument \"{arg}\".");
      }
      string name;
      string? inlineValue = null;
      var equals = arg.IndexOf('=');
      if (equals > 0) {
        name = arg[2..equals];
        inlineValue = arg[(equals + 1)..];
      } else {
        name = arg[2..];
      }
      string Value() {
        if (inlineValue != null) {
          return inlineValue;
        }
        if (index + 1 >= args.Count) {
          throw new CommandLineException($"Missing value of --{name}.");
        }
        return args[++index];
      }
      void NoValue() {
        if (inlineValue != null) {
          throw new CommandLineException($"--{name} doesn't take a value.");
        }
      }

      switch (name) {
        case "help": NoValue(); options = options with { Command = BenchCommand.Help }; break;
        case "non-interactive": NoValue(); options = options with { NonInteractive = true }; break;
        case "disk": options = options with { Disk = Value() }; break;
        case "corpora-dir": options = options with { CorporaDirectory = Value() }; break;
        case "corpus": options = options with { Corpus = ParseCorpus(Value()) }; break;
        case "symlinks": options = options with { Symlinks = ParseYesNo(name, Value()) }; break;
        case "no-symlinks": NoValue(); options = options with { Symlinks = false }; break;
        case "change-link-targets": NoValue(); options = options with { ChangeLinkTargets = true }; break;
        case "include-virtual-displays": NoValue(); options = options with { IncludeVirtualDisplays = true }; break;
        case "server": options = options with { Server = ParseServer(Value()) }; break;
        case "scenarios": options = options with { Scenarios = ParseScenarios(Value()) }; break;
        case "repeat": options = options with { Repeat = ParsePositive(name, Value()) }; break;
        case "cache": options = options with { Cache = ParseCache(Value()) }; break;
        case "flush-cache": options = options with { FlushCache = ParseYesNo(name, Value()) ? YesNoAsk.Yes : YesNoAsk.No }; break;
        case "list-filters": options = options with { ListFilters = ParseYesNo(name, Value()) ? YesNoAsk.Yes : YesNoAsk.No }; break;
        case "keep-server": NoValue(); options = options with { KeepServer = true }; break;
        case "no-keep-server": NoValue(); options = options with { KeepServer = false }; break;
        case "changes": options = options with { ChangeCounts = ParseIntList(name, Value(), allowZero: false) }; break;
        case "threads": options = options with { ThreadCounts = ParseIntList(name, Value(), allowZero: true) }; break;
        case "results-dir": options = options with { ResultsDirectory = Value() }; break;
        case "label": options = options with { Label = Value() }; break;
        case "rerun-script": options = options with { RerunScript = Value() }; break;
        case "data-dir": options = options with { DataDirectory = Value() }; break;
        case "machine-label": options = options with { MachineLabel = Value() }; break;
        case "report-dir": options = options with { ReportDirectory = Value() }; break;
        case "redact-security": NoValue(); options = options with { RedactSecurity = true }; break;
        case "output": options = options with { Output = Value() }; break;
        case "corpora" or "caches" or "servers" or "results" or "all": NoValue(); cleanup.Add(name); break;
        case "yes" or "y": NoValue(); options = options with { Yes = true }; break;
        case "json": NoValue(); options = options with { Json = true }; break;
        default: throw new CommandLineException($"Unknown option --{name}.");
      }
    }
    if (options.Command is BenchCommand.Compare or BenchCommand.Report && files.Count == 0) {
      throw new CommandLineException($"{options.Command.ToString().ToLowerInvariant()}: no result files.");
    }
    if (cleanup.Count > 0 && options.Command != BenchCommand.Cleanup) {
      throw new CommandLineException($"--{cleanup[0]} is an option of \"cleanup\".");
    }
    return options with { Files = files, CleanupTargets = cleanup.Count > 0 ? cleanup : null };
  }

  private static BenchCommand ParseCommand(string text) => text.ToLowerInvariant() switch {
    "run" => BenchCommand.Run,
    "compare" => BenchCommand.Compare,
    "report" => BenchCommand.Report,
    "cleanup" => BenchCommand.Cleanup,
    "inventory" => BenchCommand.Inventory,
    "help" => BenchCommand.Help,
    _ => throw new CommandLineException($"Unknown command \"{text}\" (expected run, compare, cleanup, inventory or help)."),
  };

  /// <summary>The canonical name of a corpus size (400k, 1M, 2M, small, medium).</summary>
  public static string ParseCorpus(string text) => text.ToLowerInvariant() switch {
    "400k" or "large" => "400k",
    "1m" or "xlarge" => "1M",
    "2m" or "xxlarge" => "2M",
    "small" => "small",
    "medium" => "medium",
    _ => throw new CommandLineException($"Unknown corpus \"{text}\" (expected 400k, 1M or 2M)."),
  };

  public static string ParseServer(string text) {
    if (text.StartsWith("release:", StringComparison.OrdinalIgnoreCase)) {
      var tag = text["release:".Length..];
      if (tag.Length == 0) {
        throw new CommandLineException("--server release:<tag>: missing tag (or \"latest\").");
      }
      return "release:" + tag;
    }
    return text.ToLowerInvariant() is "auto" or "repo" or "vscode" ? text.ToLowerInvariant() : text;
  }

  public static IReadOnlyList<Scenario> ParseScenarios(string text) {
    var result = new List<Scenario>();
    foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      if (part.Equals("all", StringComparison.OrdinalIgnoreCase)) {
        result.AddRange(BenchOptions.DefaultScenarios);
        continue;
      }
      var scenario = part.ToLowerInvariant() switch {
        "cold" or "1" => Scenario.Cold,
        "save" or "2" => Scenario.Save,
        "restart" or "3" => Scenario.Restart,
        "live" or "4" => Scenario.Live,
        "offline" or "5" => Scenario.Offline,
        "search" or "6" => Scenario.Search,
        "rawfs" or "raw" or "7" => Scenario.RawFs,
        "threads" or "8" => Scenario.Threads,
        _ => throw new CommandLineException($"Unknown scenario \"{part}\" (expected cold, save, restart, live, offline, search, rawfs, threads)."),
      };
      if (!result.Contains(scenario)) {
        result.Add(scenario);
      }
    }
    if (result.Count == 0) {
      throw new CommandLineException("--scenarios: empty list.");
    }
    // Always run in the order of the design (each scenario may depend on the previous ones).
    return result.Distinct().OrderBy(s => s).ToList();
  }

  public static string ScenarioName(Scenario scenario) => scenario switch {
    Scenario.RawFs => "rawfs",
    _ => scenario.ToString().ToLowerInvariant(),
  };

  private static CacheMode ParseCache(string text) => text.ToLowerInvariant() switch {
    "warm" => CacheMode.Warm,
    "cold" => CacheMode.Cold,
    _ => throw new CommandLineException($"--cache: expected warm or cold, not \"{text}\"."),
  };

  private static bool ParseYesNo(string name, string text) => text.ToLowerInvariant() switch {
    "yes" or "y" or "true" or "1" => true,
    "no" or "n" or "false" or "0" => false,
    _ => throw new CommandLineException($"--{name}: expected yes or no, not \"{text}\"."),
  };

  private static int ParsePositive(string name, string text) {
    if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0) {
      throw new CommandLineException($"--{name}: expected a positive number, not \"{text}\".");
    }
    return value;
  }

  private static IReadOnlyList<int> ParseIntList(string name, string text, bool allowZero) {
    var result = new List<int>();
    foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)) {
      var number = part.Replace("_", "");
      var multiplier = 1;
      if (number.EndsWith('k') || number.EndsWith('K')) {
        multiplier = 1000;
        number = number[..^1];
      }
      if (!int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0 || (value == 0 && !allowZero)) {
        throw new CommandLineException($"--{name}: invalid number \"{part}\".");
      }
      result.Add(value * multiplier);
    }
    if (result.Count == 0) {
      throw new CommandLineException($"--{name}: empty list.");
    }
    return result;
  }
}
