// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using VsChromium.Bench.Platform;

namespace VsChromium.Bench.Servers;

/// <summary>
/// The benchmark features supported by a server, detected without assuming a version: the options
/// listed in the usage text of the server (printed for an unknown argument, e.g. "--help"), and the
/// "bench/*" requests listed by "server/initialize" ("bench": { "requests": [...] }) when the server
/// runs with "--bench". Older
/// servers have none of them: the bench then uses the existing protocol only.
/// </summary>
public sealed partial record ServerCapabilities(IReadOnlySet<string> Options, IReadOnlySet<string> BenchRequests) {
  public const string BenchOption = "--bench";
  public const string ThreadsOption = "--threads";
  public const string WatcherDelayOption = "--watcher-delay-ms";
  public const string NoJournalOption = "--no-journal";
  public const string SaveCacheRequest = "bench/saveCache";
  public const string PhasesRequest = "bench/phases";

  public static readonly ServerCapabilities None = new(new HashSet<string>(), new HashSet<string>());

  public bool Bench => Options.Contains(BenchOption);
  public bool Threads => Options.Contains(ThreadsOption);
  public bool WatcherDelay => Options.Contains(WatcherDelayOption);
  public bool NoJournal => Options.Contains(NoJournalOption);
  public bool SaveCache => Bench && BenchRequests.Contains(SaveCacheRequest);
  public bool Phases => Bench && BenchRequests.Contains(PhasesRequest);

  /// <summary>The supported bench features, for the results ("--threads", "bench/saveCache", ...).</summary>
  public List<string> ToList() =>
    Options.Where(o => o is BenchOption or ThreadsOption or WatcherDelayOption or NoJournalOption).Order()
      .Concat(BenchRequests.Order()).ToList();

  /// <summary>Runs "&lt;server&gt; --help" and parses the options of its usage text.</summary>
  public static ServerCapabilities Probe(string serverPath) {
    var result = ProcessRunner.Run(serverPath, ["--help"], TimeSpan.FromSeconds(30));
    return result == null ? None : FromUsage(result.Output + "\n" + result.Error);
  }

  /// <summary>The options ("--name") of a usage text.</summary>
  public static ServerCapabilities FromUsage(string usage) =>
    new(OptionPattern().Matches(usage).Select(m => m.Value).ToHashSet(), new HashSet<string>());

  /// <summary>Adds the "bench/*" requests listed by "server/initialize" ("bench": { "requests": [...] }).</summary>
  public ServerCapabilities WithInitializeResult(JsonObject initializeResult) {
    var requests = (initializeResult["bench"]?["requests"] as JsonArray ?? []).Select(n => (string?)n).OfType<string>().ToHashSet();
    return this with { BenchRequests = requests };
  }

  [GeneratedRegex(@"(?<![\w-])--[a-z][a-z0-9-]*")]
  private static partial Regex OptionPattern();
}
