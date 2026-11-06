// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using VsChromium.Bench.Cli;
using VsChromium.Bench.Platform;
using VsChromium.Bench.Results;
using VsChromium.Bench.Servers;
using VsChromium.Bench.Shared.Corpus;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Bench.Scenarios;

/// <summary>Everything a run needs (resolved options).</summary>
public sealed record RunContext(
  GeneratedCorpus Corpus,
  string ServerPath,
  ServerCapabilities Capabilities,
  string CacheDirectory,
  IReadOnlyList<Scenario> Scenarios,
  int Repeat,
  bool FlushCache,
  IReadOnlyList<int> ChangeCounts,
  IReadOnlyList<int> ThreadCounts,
  bool KeepServer,
  Action<string> Log,
  CancellationToken Cancellation,
  bool ChangeLinkTargets = false);

/// <summary>
/// Runs the scenarios of the design (1-8) with the existing protocol of the server, using the bench
/// features of the server only when it supports them (see <see cref="ServerCapabilities"/>).
/// </summary>
public sealed class ScenarioRunner(RunContext context) : IAsyncDisposable {
  private static readonly TimeSpan LoadTimeout = TimeSpan.FromHours(2);
  private static readonly TimeSpan UpdateTimeout = TimeSpan.FromMinutes(15);
  private static readonly TimeSpan QuiescenceTimeout = TimeSpan.FromMinutes(15);

  private readonly Stopwatch _clock = Stopwatch.StartNew();
  private ServerHost? _kept;
  private FileChanges? _changes;
  private bool _cacheReady;

  private string Root => Path.GetFullPath(context.Corpus.RootPath);
  private string ProjectFile => Path.Combine(Root, "vs-chromium-project.txt");
  private CancellationToken Cancellation => context.Cancellation;

  private void Log(string message) => context.Log(message);

  public async Task<List<ScenarioResult>> RunAsync() {
    var results = new List<ScenarioResult>();
    foreach (var scenario in context.Scenarios) {
      Cancellation.ThrowIfCancellationRequested();
      var name = CommandLineParser.ScenarioName(scenario);
      Log($"== Scenario {(int)scenario + 1}: {name}");
      ScenarioResult result;
      try {
        result = scenario switch {
          Scenario.Cold => await ColdAsync(),
          Scenario.Save => await SaveAsync(),
          Scenario.Restart => await RestartAsync(),
          Scenario.Live => await LiveAsync(),
          Scenario.Offline => await OfflineAsync(),
          Scenario.Search => await SearchAsync(),
          Scenario.RawFs => await RawFsAsync(),
          Scenario.Threads => await ThreadsAsync(),
          _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
      } catch (Exception e) when (e is not OperationCanceledException) {
        Log($"Scenario {name} failed: {e.Message}");
        result = new ScenarioResult { Name = name, Error = e.Message };
        await StopKeptAsync();
      }
      results.Add(result with { Name = name });
    }
    return results;
  }

  // -----------------------------------------------------------------------------------------------
  // Helpers

  private void FlushIfCold() {
    if (!context.FlushCache) {
      return;
    }
    var error = CacheFlusher.Flush();
    if (error != null) {
      throw new InvalidOperationException($"Cannot flush the OS file cache: {error}");
    }
  }

  private Task<ServerHost> StartAsync(string? cacheDirectory, IReadOnlyList<string>? extraArguments = null) =>
    ServerHost.StartAsync(context.ServerPath, cacheDirectory, context.Capabilities, _clock, extraArguments);

  private async Task StopKeptAsync() {
    if (_kept != null) {
      await _kept.DisposeAsync();
      _kept = null;
    }
  }

  private static void DeleteDirectory(string path) {
    if (Directory.Exists(path)) {
      Directory.Delete(path, recursive: true);
    }
  }

  private static long DirectorySize(string path) =>
    Directory.Exists(path) ? new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length) : 0;

  private static bool IsFromCache(JsonObject loaded) => (bool?)loaded["fromCache"] == true;

  private static void ThrowIfError(ReceivedNotification loaded) {
    if (loaded.Params["error"] is JsonObject error) {
      throw new InvalidOperationException($"Index load failed: {(string?)error["message"] ?? error.ToJsonString()}");
    }
  }

  private static string Files(int count) => count == 1 ? "1 file" : $"{count.ToString("n0", CultureInfo.InvariantCulture)} files";

  /// <summary>Registers the project and waits for its complete index (cold load).</summary>
  private async Task<(ReceivedNotification Loaded, ProcessSnapshot Before, ProcessSnapshot After)> IndexColdAsync(ServerHost host) {
    var cursor = host.NotificationCount;
    var before = host.Snapshot();
    await host.RequestAsync(ServerProtocol.RegisterFile(ProjectFile));
    var (loaded, _) = await host.WaitForNotificationAsync("index/loaded", cursor, null, LoadTimeout, Cancellation);
    var after = host.Snapshot();
    ThrowIfError(loaded);
    return (loaded, before, after);
  }

  /// <summary>
  /// Registers the project on a server started with a cache, waits for the index of the cache
  /// (searchable) and for its verification.
  /// </summary>
  private async Task<(TimeSpan? Searchable, TimeSpan Verified, bool FromCache, ProcessSnapshot Before, ProcessSnapshot After)> LoadFromCacheAsync(ServerHost host) {
    var cursor = host.NotificationCount;
    var before = host.Snapshot();
    await host.RequestAsync(ServerProtocol.RegisterFile(ProjectFile));
    var (first, next) = await host.WaitForNotificationAsync("index/loaded", cursor, null, LoadTimeout, Cancellation);
    ThrowIfError(first);
    if (!IsFromCache(first.Params)) {
      var afterCold = host.Snapshot();
      return (null, first.Time - before.Elapsed, false, before, afterCold);
    }
    var (verified, _) = await host.WaitForNotificationAsync("index/loaded", next, p => !IsFromCache(p), LoadTimeout, Cancellation);
    ThrowIfError(verified);
    var after = host.Snapshot();
    return (first.Time - before.Elapsed, verified.Time - before.Elapsed, true, before, after);
  }

  private static void AddProcessMetrics(MetricSet metrics, ProcessSnapshot before, ProcessSnapshot after) {
    metrics.Add("server CPU user", "s", (after.UserCpu - before.UserCpu).TotalSeconds);
    metrics.Add("server CPU kernel", "s", (after.KernelCpu - before.KernelCpu).TotalSeconds);
    metrics.Add("server peak working set", "bytes", after.PeakWorkingSet);
    metrics.Add("server private bytes", "bytes", after.PrivateBytes);
  }

  /// <summary>Per-phase timings of the last load, when the server supports "bench/phases".</summary>
  private async Task AddPhasesAsync(ServerHost host, MetricSet metrics, string prefix) {
    if (!host.Capabilities.Phases) {
      return;
    }
    try {
      var phases = await host.RequestAsync(new ServerRequest(ServerCapabilities.PhasesRequest, new JsonObject { ["projectPath"] = Root }));
      // "phases": the last duration of each operation of the server.
      foreach (var phase in (phases["phases"] as JsonArray ?? []).OfType<JsonObject>()) {
        if ((string?)phase["name"] is { } phaseName && Stats.Double(phase, "lastMs") is { } lastMs) {
          metrics.Add($"{prefix}{phaseName}", "ms", lastMs);
        }
      }
      foreach (var (name, value) in Flatten(phases, "")) {
        if (name.StartsWith("cpu", StringComparison.Ordinal)) {
          continue; // CPU time since the start of the process: measured per phase from outside.
        }
        metrics.Add($"{prefix}{name}", name.EndsWith("Ms", StringComparison.Ordinal) ? "ms" : "count", value);
      }
    } catch (ServerErrorException e) {
      Log($"bench/phases failed: {e.Message}");
    }
  }

  private static IEnumerable<(string Name, double Value)> Flatten(JsonNode? node, string prefix) {
    switch (node) {
      case JsonObject obj:
        foreach (var (key, value) in obj) {
          foreach (var item in Flatten(value, prefix.Length == 0 ? key : $"{prefix}.{key}")) {
            yield return item;
          }
        }
        break;
      case JsonValue value when value.TryGetValue<double>(out var d):
        yield return (prefix, d);
        break;
    }
  }

  /// <summary>Makes sure the cache directory has a saved index of the corpus (cold index + save, unmeasured).</summary>
  private async Task EnsureCacheAsync() {
    if (_cacheReady && Directory.Exists(context.CacheDirectory)) {
      return;
    }
    Log("Preparing the index cache (cold index and save, not measured)...");
    await StopKeptAsync();
    DeleteDirectory(context.CacheDirectory);
    await using var host = await StartAsync(context.CacheDirectory);
    await IndexColdAsync(host);
    await WaitForSaveAsync(host);
    await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
    _cacheReady = true;
  }

  /// <summary>A server with the index of the corpus loaded and verified: the kept one, or a new one from the cache.</summary>
  private async Task<ServerHost> GetReadyServerAsync() {
    if (_kept != null) {
      return _kept;
    }
    await EnsureCacheAsync();
    var host = await StartAsync(context.CacheDirectory);
    await LoadFromCacheAsync(host);
    await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
    _kept = host;
    return host;
  }

  /// <summary>Waits for the first save of the cache; returns the duration (ms) and bytes reported by the server.</summary>
  private async Task<(double DurationMs, long Bytes)> WaitForSaveAsync(ServerHost host) {
    if (host.Capabilities.SaveCache) {
      var result = await host.RequestAsync(new ServerRequest(ServerCapabilities.SaveCacheRequest, new JsonObject()), LoadTimeout);
      return (Stats.Double(result, "durationMs") ?? 0, (long)(Stats.Double(result, "bytes") ?? 0));
    }
    var deadline = host.Now + LoadTimeout;
    while (host.Now < deadline) {
      var stats = await host.StatisticsAsync();
      var cache = stats["indexCache"] as JsonObject;
      if (cache == null || !((bool?)cache["enabled"] ?? false)) {
        throw new InvalidOperationException("The server has no index cache.");
      }
      if (((int?)cache["writeCount"] ?? 0) >= 1) {
        return ((double?)cache["lastWriteMs"] ?? 0, (long?)cache["lastWriteBytes"] ?? 0);
      }
      await Task.Delay(200, Cancellation);
    }
    throw new TimeoutException("The index cache was not saved.");
  }

  // -----------------------------------------------------------------------------------------------
  // 1. Cold index, 2. Save

  private readonly MetricSet _saveMetrics = new();

  private async Task<ScenarioResult> ColdAsync() {
    var metrics = new MetricSet();
    var saveSelected = context.Scenarios.Contains(Scenario.Save);
    for (var i = 0; i < context.Repeat; i++) {
      await StopKeptAsync();
      DeleteDirectory(context.CacheDirectory);
      _cacheReady = false;
      FlushIfCold();
      await using var host = await StartAsync(context.CacheDirectory);
      var (loaded, before, after) = await IndexColdAsync(host);
      var wall = (loaded.Time - before.Elapsed).TotalSeconds;
      var stats = await host.StatisticsAsync();
      var details = await host.ProjectDetailsAsync(Root);
      var fileCount = (int?)stats["fileCount"] ?? 0;
      metrics.Add("time to index", "s", wall);
      metrics.Add("files per second", "files/s", fileCount / Math.Max(wall, 1e-6));
      metrics.Add("scan", "s", (Stats.Double(details, "operations", "initialLoad", "scanMs") ?? Stats.OperationLastMs(stats, "FileSystemScan") ?? 0) / 1000);
      metrics.Add("file load", "s", (Stats.OperationLastMs(stats, "FileDatabaseUpdate.Load") ?? 0) / 1000);
      metrics.Add("indexed files", "count", fileCount);
      AddProcessMetrics(metrics, before, after);
      await AddPhasesAsync(host, metrics, "phase ");
      Log($"  cold index {i + 1}/{context.Repeat}: {wall:n1} s, {fileCount:n0} files");

      // 2. Save of the cache (by the same server).
      var saveBefore = host.Snapshot();
      var (saveMs, saveBytes) = await WaitForSaveAsync(host);
      await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
      var saveAfter = host.Snapshot();
      _cacheReady = true;
      if (saveSelected) {
        var size = DirectorySize(context.CacheDirectory);
        _saveMetrics.Add("save", "s", saveMs / 1000);
        _saveMetrics.Add("bytes written", "bytes", saveBytes);
        _saveMetrics.Add("cache size on disk", "bytes", size);
        _saveMetrics.Add("server CPU user", "s", (saveAfter.UserCpu - saveBefore.UserCpu).TotalSeconds);
        _saveMetrics.Add("server CPU kernel", "s", (saveAfter.KernelCpu - saveBefore.KernelCpu).TotalSeconds);
        Log($"  save {i + 1}/{context.Repeat}: {saveMs / 1000:n1} s, cache {size / 1048576.0:n0} MB");
      }
    }
    return new ScenarioResult { Metrics = metrics.ToMetrics() };
  }

  private async Task<ScenarioResult> SaveAsync() {
    if (!context.Scenarios.Contains(Scenario.Cold)) {
      // Measured with the cold index: run it (unmeasured cold index, measured save).
      for (var i = 0; i < context.Repeat; i++) {
        await StopKeptAsync();
        DeleteDirectory(context.CacheDirectory);
        await using var host = await StartAsync(context.CacheDirectory);
        await IndexColdAsync(host);
        var before = host.Snapshot();
        var (saveMs, saveBytes) = await WaitForSaveAsync(host);
        await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
        var after = host.Snapshot();
        _cacheReady = true;
        _saveMetrics.Add("save", "s", saveMs / 1000);
        _saveMetrics.Add("bytes written", "bytes", saveBytes);
        _saveMetrics.Add("cache size on disk", "bytes", DirectorySize(context.CacheDirectory));
        _saveMetrics.Add("server CPU user", "s", (after.UserCpu - before.UserCpu).TotalSeconds);
        _saveMetrics.Add("server CPU kernel", "s", (after.KernelCpu - before.KernelCpu).TotalSeconds);
      }
    }
    var result = new ScenarioResult { Metrics = _saveMetrics.ToMetrics() };
    if (!context.Capabilities.SaveCache) {
      result.Notes.Add("save duration and size reported by the server (server/statistics indexCache) after the save policy (bench/saveCache not supported)");
    }
    return await Task.FromResult(result);
  }

  // -----------------------------------------------------------------------------------------------
  // 3. Restart from the cache

  private async Task<ScenarioResult> RestartAsync() {
    await EnsureCacheAsync();
    var metrics = new MetricSet();
    var info = new Dictionary<string, string>();
    var notes = new List<string>();
    var keepAfter = context.KeepServer && (context.Scenarios.Contains(Scenario.Live) || context.Scenarios.Contains(Scenario.Search) || context.Scenarios.Contains(Scenario.Offline));
    for (var i = 0; i < context.Repeat; i++) {
      await StopKeptAsync();
      FlushIfCold();
      var host = await StartAsync(context.CacheDirectory);
      try {
        var (searchable, verified, fromCache, before, after) = await LoadFromCacheAsync(host);
        if (!fromCache) {
          notes.Add($"repeat {i + 1}: the index cache was not used (cold load)");
        }
        var details = await host.ProjectDetailsAsync(Root);
        var verification = details["operations"]?["lastVerification"] as JsonObject;
        if (searchable is { } s) {
          metrics.Add("time to searchable", "s", s.TotalSeconds);
        }
        metrics.Add("time to verified", "s", verified.TotalSeconds);
        if (verification != null) {
          metrics.Add("verification", "s", (Stats.Double(verification, "durationMs") ?? 0) / 1000);
          metrics.Add("journal records", "count", Stats.Double(verification, "journalRecordCount") ?? 0);
          metrics.Add("directories listed", "count", Stats.Double(verification, "listedDirectoryCount") ?? 0);
          info["verification method"] = Stats.String(verification, "method") ?? "?";
          if (Stats.String(verification, "fallbackReason") is { } reason) {
            info["fallback reason"] = reason;
          }
        }
        AddProcessMetrics(metrics, before, after);
        await AddPhasesAsync(host, metrics, "phase ");
        Log($"  restart {i + 1}/{context.Repeat}: searchable {searchable?.TotalSeconds:n2} s, verified {verified.TotalSeconds:n2} s ({info.GetValueOrDefault("verification method")})");
        await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
      } catch {
        await host.DisposeAsync();
        throw;
      }
      if (i == context.Repeat - 1 && keepAfter) {
        _kept = host;
      } else {
        await host.DisposeAsync();
      }
    }
    return new ScenarioResult { Metrics = metrics.ToMetrics(), Info = info, Notes = notes };
  }

  // -----------------------------------------------------------------------------------------------
  // 4. Live changes

  private FileChanges Changes {
    get {
      if (_changes == null) {
        Log("Listing the text files of the corpus (file changes)...");
        _changes = FileChanges.Scan(Root, context.ChangeLinkTargets);
      }
      return _changes;
    }
  }

  private async Task<ScenarioResult> LiveAsync() {
    var metrics = new MetricSet();
    var info = new Dictionary<string, string> {
      ["watcher delay"] = context.Capabilities.WatcherDelay ? "server default (0.5 s debounce; the server supports --watcher-delay-ms)" : "server default (0.5 s debounce, not configurable by this server)",
    };
    var notes = new List<string> { "time from the last write to the index update with all the changes (index/loaded notification); includes the watcher delay" };
    var host = await GetReadyServerAsync();
    for (var i = 0; i < context.Repeat; i++) {
      foreach (var count in context.ChangeCounts) {
        var files = Changes.Select(count, salt: 1 + i);
        var marker = $"VSCB_BENCH_LIVE_{files.Count}_{i}_{Environment.TickCount64}";
        await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
        var statsBefore = await host.StatisticsAsync();
        var snapshotBefore = host.Snapshot();
        var cursor = host.NotificationCount;
        var writeStart = host.Now;
        var backup = FileChanges.Modify(files, marker);
        var lastWrite = host.Now;
        TimeSpan latency;
        try {
          latency = await WaitForHitsAsync(host, marker, Changes.ExpectedHits(files), cursor, lastWrite) - lastWrite;
        } finally {
          backup.Restore();
        }
        var snapshotAfter = host.Snapshot();
        var statsAfter = await host.StatisticsAsync();
        var label = Files(files.Count);
        metrics.Add($"{label}: write to indexed", "s", latency.TotalSeconds);
        metrics.Add($"{label}: writes", "s", (lastWrite - writeStart).TotalSeconds);
        metrics.Add($"{label}: server scan + update", "s",
          (Stats.OperationTotalMs(statsAfter, "FileSystemScan") - Stats.OperationTotalMs(statsBefore, "FileSystemScan") +
           Stats.OperationTotalMs(statsAfter, "FileDatabaseUpdate") - Stats.OperationTotalMs(statsBefore, "FileDatabaseUpdate")) / 1000);
        metrics.Add($"{label}: server CPU", "s", (snapshotAfter.UserCpu + snapshotAfter.KernelCpu - snapshotBefore.UserCpu - snapshotBefore.KernelCpu).TotalSeconds);
        Log($"  live {label} ({i + 1}/{context.Repeat}): {latency.TotalSeconds:n2} s");
        // Restored: wait until the index no longer has the marker.
        var restoreCursor = host.NotificationCount;
        await WaitForHitsAsync(host, marker, 0, restoreCursor, host.Now);
      }
    }
    await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
    if (!context.KeepServer) {
      await StopKeptAsync();
    }
    return new ScenarioResult { Metrics = metrics.ToMetrics(), Info = info, Notes = notes };
  }

  /// <summary>
  /// Waits for the index update after which a search finds <paramref name="expected"/> hits of the
  /// marker; returns the time of its "index/loaded" notification.
  /// </summary>
  private async Task<TimeSpan> WaitForHitsAsync(ServerHost host, string marker, int expected, int cursor, TimeSpan notBefore) {
    var deadline = host.Now + UpdateTimeout;
    while (true) {
      var remaining = deadline - host.Now;
      if (remaining <= TimeSpan.Zero) {
        throw new TimeoutException($"The index was not updated within {UpdateTimeout} ({expected} hits of the marker expected, {await host.CountHitsAsync(marker)} found).");
      }
      ReceivedNotification loaded;
      try {
        (loaded, cursor) = await host.WaitForNotificationAsync("index/loaded", cursor, null, remaining < TimeSpan.FromSeconds(30) ? remaining : TimeSpan.FromSeconds(30), Cancellation);
      } catch (TimeoutException) when (!host.Process.HasExited) {
        // No notification for a while: check anyway (e.g. the update was published before the notification cursor).
        if (await host.CountHitsAsync(marker) == expected) {
          return host.Now;
        }
        continue;
      }
      if (loaded.Time < notBefore) {
        continue;
      }
      if (await host.CountHitsAsync(marker) == expected) {
        return loaded.Time;
      }
    }
  }

  // -----------------------------------------------------------------------------------------------
  // 5. Changes while stopped

  private async Task<ScenarioResult> OfflineAsync() {
    var metrics = new MetricSet();
    var info = new Dictionary<string, string>();
    var notes = new List<string> { "server stopped, files modified, server restarted: time to the verified index (catch-up)" };
    var variants = new List<(string Suffix, IReadOnlyList<string> Arguments)> { ("", []) };
    if (context.Capabilities.NoJournal) {
      variants.Add((" (--no-journal)", [ServerCapabilities.NoJournalOption]));
    }
    for (var i = 0; i < context.Repeat; i++) {
      foreach (var count in context.ChangeCounts) {
        foreach (var (suffix, arguments) in variants) {
          var host = await GetReadyServerAsync();
          await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
          await StopKeptAsync();
          var files = Changes.Select(count, salt: 101 + i);
          var marker = $"VSCB_BENCH_OFFLINE_{files.Count}_{i}_{Environment.TickCount64}";
          var backup = FileChanges.Modify(files, marker);
          ServerHost? restarted = null;
          try {
            FlushIfCold();
            restarted = await StartAsync(context.CacheDirectory, arguments);
            var (searchable, verified, fromCache, before, after) = await LoadFromCacheAsync(restarted);
            var hits = await restarted.CountHitsAsync(marker);
            var details = await restarted.ProjectDetailsAsync(Root);
            var stats = await restarted.StatisticsAsync();
            var verification = details["operations"]?["lastVerification"] as JsonObject;
            var label = Files(files.Count) + suffix;
            metrics.Add($"{label}: catch-up", "s", verified.TotalSeconds);
            if (searchable is { } s) {
              metrics.Add($"{label}: searchable", "s", s.TotalSeconds);
            }
            metrics.Add($"{label}: verification", "s", (Stats.Double(verification, "durationMs") ?? 0) / 1000);
            metrics.Add($"{label}: directories listed", "count", Stats.Double(verification, "listedDirectoryCount") ?? 0);
            metrics.Add($"{label}: files read from disk", "count", Stats.Double(stats, "indexCache", "lastLoadFilesFromDisk") ?? 0);
            metrics.Add($"{label}: server CPU", "s", (after.UserCpu + after.KernelCpu - before.UserCpu - before.KernelCpu).TotalSeconds);
            info[$"{label}: verification method"] = Stats.String(verification, "method") ?? (fromCache ? "?" : "cold load");
            if (hits != Changes.ExpectedHits(files)) {
              notes.Add($"{label}, repeat {i + 1}: {hits} hits of the marker after the catch-up, expected {Changes.ExpectedHits(files)}");
            }
            Log($"  offline {label} ({i + 1}/{context.Repeat}): catch-up {verified.TotalSeconds:n2} s ({info[$"{label}: verification method"]})");
          } finally {
            backup.Restore();
          }
          // Restored while the server runs: wait for the update and the save of the cache.
          var cursor = restarted.NotificationCount;
          await WaitForHitsAsync(restarted, marker, 0, cursor, restarted.Now);
          await restarted.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
          if (arguments.Count == 0) {
            _kept = restarted;
          } else {
            await restarted.DisposeAsync();
          }
        }
      }
    }
    if (!context.KeepServer) {
      await StopKeptAsync();
    }
    return new ScenarioResult { Metrics = metrics.ToMetrics(), Info = info, Notes = notes };
  }

  // -----------------------------------------------------------------------------------------------
  // 6. Search latency

  private async Task<ScenarioResult> SearchAsync() {
    var unique = CodeBaseGenerator.UniqueMarkerPrefix + "00";
    var queries = new (string Name, ServerRequest Request, long? ExpectedHits)[] {
      ("literal, ignore case", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker.ToLowerInvariant(), maxResults: 10_000_000),
        context.Corpus.Manifest.Markers.FirstOrDefault(m => m.Text == CodeBaseGenerator.CommonMarker)?.HitCount),
      ("literal, case-sensitive", ServerProtocol.SearchCode("WebContents", matchCase: true), null),
      ("unique marker", ServerProtocol.SearchCode(unique, maxResults: 10_000_000, matchCase: true), 1),
      ("regex", ServerProtocol.SearchCode(@"void \w+::\w+\(", matchCase: true, regex: true), null),
      ("file path", ServerProtocol.SearchFilePaths("*_unittest.cc"), null),
    };
    var metrics = new MetricSet();
    var notes = new List<string> { "median of 10 runs of each query (after a warm-up run), measured by the client" };
    var host = await GetReadyServerAsync();
    await host.WaitForQuiescenceAsync(QuiescenceTimeout, Cancellation);
    for (var i = 0; i < context.Repeat; i++) {
      foreach (var (name, request, expected) in queries) {
        var warmup = await host.Client.RequestAsync(request);
        var hits = warmup.Data["hitCount"]?.GetValue<long>();
        if (expected is { } e && hits != e && i == 0) {
          notes.Add($"{name}: {hits} hits, expected {e}");
        }
        var timings = new List<double>();
        for (var run = 0; run < 10; run++) {
          timings.Add((await host.Client.RequestAsync(request)).Elapsed.TotalMilliseconds);
        }
        metrics.Add($"{name}", "ms", Statistics.Median(timings));
      }
    }
    if (!context.KeepServer) {
      await StopKeptAsync();
    }
    return new ScenarioResult { Metrics = metrics.ToMetrics(), Notes = notes };
  }

  // -----------------------------------------------------------------------------------------------
  // 7. Raw file system

  private async Task<ScenarioResult> RawFsAsync() {
    await StopKeptAsync();
    Log("Sampling the corpus (raw file system)...");
    var raw = RawFileSystem.Prepare(Root);
    var metrics = new MetricSet();
    for (var i = 0; i < context.Repeat; i++) {
      Cancellation.ThrowIfCancellationRequested();
      FlushIfCold();
      var t = raw.Run();
      metrics.Add("directory listing, per directory", "us", t.ListMicroseconds);
      metrics.Add("attribute-only open, per file", "us", t.AttributesMicroseconds);
      metrics.Add("open + read + close, per file", "us", t.ReadMicroseconds);
      metrics.Add("read throughput", "MB/s", t.ReadBytes / 1048576.0 / Math.Max(1e-9, t.ReadMicroseconds * t.FileCount / 1e6));
      metrics.Add("listing of all directories, all cores", "s", t.ParallelListSeconds);
      Log($"  raw {i + 1}/{context.Repeat}: list {t.ListMicroseconds:n0} us/dir, attributes {t.AttributesMicroseconds:n0} us/file, read {t.ReadMicroseconds:n0} us/file");
    }
    var result = new ScenarioResult { Metrics = metrics.ToMetrics() };
    result.Info["sample"] = $"{raw.DirectorySampleCount:n0} directories, {raw.FileSampleCount:n0} files (single thread)";
    if (!OperatingSystem.IsWindows()) {
      result.Info["attribute-only open"] = "stat (no open on this OS)";
    }
    return await Task.FromResult(result);
  }

  // -----------------------------------------------------------------------------------------------
  // 8. Thread scaling

  private async Task<ScenarioResult> ThreadsAsync() {
    if (!context.Capabilities.Threads) {
      return new ScenarioResult { Skipped = true, Notes = ["the server doesn't support --threads"] };
    }
    await StopKeptAsync();
    var metrics = new MetricSet();
    var cache = context.CacheDirectory + "-threads";
    foreach (var threads in context.ThreadCounts) {
      var label = threads == 0 ? "all threads" : threads == 1 ? "1 thread" : $"{threads} threads";
      for (var i = 0; i < context.Repeat; i++) {
        DeleteDirectory(cache);
        FlushIfCold();
        await using var host = await StartAsync(cache, [ServerCapabilities.ThreadsOption, threads.ToString(CultureInfo.InvariantCulture)]);
        var (loaded, before, after) = await IndexColdAsync(host);
        metrics.Add($"{label}: time to index", "s", (loaded.Time - before.Elapsed).TotalSeconds);
        metrics.Add($"{label}: server CPU", "s", (after.UserCpu + after.KernelCpu - before.UserCpu - before.KernelCpu).TotalSeconds);
        Log($"  threads {label} ({i + 1}/{context.Repeat}): {(loaded.Time - before.Elapsed).TotalSeconds:n1} s");
      }
    }
    DeleteDirectory(cache);
    return new ScenarioResult { Metrics = metrics.ToMetrics() };
  }

  public async ValueTask DisposeAsync() {
    await StopKeptAsync();
  }
}
