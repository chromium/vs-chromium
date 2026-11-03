// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Text.Json.Nodes;
using VsChromium.Tests.EndToEnd.Corpus;
using VsChromium.Tests.EndToEnd.Server;

namespace VsChromium.Tests.EndToEnd.Benchmarks;

/// <summary>
/// Benchmarks of the server executable on a generated real-world-like code base, measuring response
/// time, CPU usage and memory usage (managed and native). See "Benchmarks" in
/// docs/portable-server/PORTING_PRINCIPLES.md.
///
/// Settings (environment variables): VSCHROMIUM_SERVER_PATH (server under test),
/// VSCHROMIUM_BENCHMARK_SIZE (small/medium/large), VSCHROMIUM_BENCHMARK_RESULTS (output directory),
/// VSCHROMIUM_BENCHMARK_BASELINE (optional result JSON file to compare with).
/// </summary>
[TestClass]
[TestCategory("Benchmark")]
public class ServerBenchmarks {
  public TestContext TestContext { get; set; } = null!;

  private sealed record QuerySpec(string Name, string Kind, ServerRequest Request, long? ExpectedHits);

  [TestMethod]
  [Timeout(4 * 60 * 60 * 1000, CooperativeCancellation = true)]
  public async Task RunBenchmark() {
    var spec = CorpusSpec.FromName(TestSettings.BenchmarkSize);
    var corpus = CorpusCache.GetOrCreate(spec, Log);
    var runs = spec == CorpusSpec.Small ? 10 : spec == CorpusSpec.Medium ? 5 : 3;
    if (int.TryParse(Environment.GetEnvironmentVariable("VSCHROMIUM_BENCHMARK_RUNS"), out var runsOverride) && runsOverride > 0)
      runs = runsOverride;
    var startedUtc = DateTime.UtcNow;
    var scenarios = new List<ScenarioResult>();
    var queries = new List<QueryResult>();

    Log($"Starting server \"{TestSettings.ServerPath}\"");
    await using var server = await JsonRpcServerClient.StartAsync(TestSettings.ServerPath);
    using var sampler = new ProcessSampler(server.Process);

    // Trigram filters are disabled by default (user option): the benchmarks measure them (the
    // "Background index" scenario), unless the server disables them (VSCHROMIUM_TRIGRAM_FILTER_BITS=0).
    await server.RequestAsync(ServerProtocol.SetProjectOptions(corpus.RootPath, true));

    // 1. Cold index: register the code base, wait until all files are loaded in the search engine.
    var start = sampler.TakeSample();
    await server.WaitForNotificationAsync("index/loaded", TimeSpan.FromHours(1),
      () => server.RequestAsync(ServerProtocol.RegisterFile(Path.Combine(corpus.RootPath, "vs-chromium-project.txt"))));
    var end = sampler.TakeSample();
    var stats = await GetStatisticsAsync(server, forceGc: false);
    scenarios.Add(new ScenarioResult("Cold index", sampler.GetUsage(start, end), stats));
    Log($"Indexed {stats["fileCount"]} files in {end.Time - start.Time}.");

    // 1b. Search index structures built in the background after the cold index (trigram filters):
    // wait until they are built (or not built at all: disabled), so that queries measure the
    // steady state.
    start = sampler.TakeSample();
    var backgroundStats = await WaitForBackgroundIndexAsync(server);
    end = sampler.TakeSample();
    scenarios.Add(new ScenarioResult("Background index", sampler.GetUsage(start, end), backgroundStats));

    // 2. Steady state: idle, then after a forced full GC.
    start = sampler.TakeSample();
    await Task.Delay(TimeSpan.FromSeconds(2));
    end = sampler.TakeSample();
    scenarios.Add(new ScenarioResult("Idle (2 s)", sampler.GetUsage(start, end), await GetStatisticsAsync(server, forceGc: false)));
    start = sampler.TakeSample();
    stats = await GetStatisticsAsync(server, forceGc: true);
    end = sampler.TakeSample();
    scenarios.Add(new ScenarioResult("After full GC", sampler.GetUsage(start, end), stats));

    // 3. Queries.
    foreach (var query in GetQueries(corpus)) {
      queries.Add(await RunQueryAsync(server, sampler, query, runs));
      Log($"{query.Kind} \"{query.Name}\": p50={queries[^1].ClientP50Ms:n1} ms, hits={queries[^1].HitCount}");
    }

    // 4. Extracts: text of the matches of the common marker, as clients do when showing results.
    queries.Add(await RunExtractsAsync(server, sampler, runs));

    // 5. Incremental updates: modify a file, create a directory with a file (the corpus is restored
    // afterwards, see RunUpdatesAsync).
    queries.AddRange(await RunUpdatesAsync(server, corpus, runs));

    scenarios.Add(new ScenarioResult("End of benchmark", sampler.GetUsage(sampler.TakeSample(), sampler.TakeSample()),
                                     await GetStatisticsAsync(server, forceGc: false)));

    var result = new BenchmarkResult(
      startedUtc,
      BenchmarkResult.CaptureEnvironment(TestSettings.ServerPath, (string?)stats["runtimeDescription"] ?? "unknown"),
      new CorpusInfo(spec.Name, corpus.RootPath, corpus.Manifest.FileCount, corpus.Manifest.TotalBytes, corpus.Manifest.SearchableFileCount),
      scenarios,
      queries);
    WriteResults(result);

    var incorrect = queries.Where(q => !q.Correct).Select(q => $"{q.Kind} \"{q.Name}\": {q.HitCount} hits, expected {q.ExpectedHitCount}").ToList();
    Assert.IsEmpty(incorrect, "Incorrect query results:\n" + string.Join("\n", incorrect));
  }

  private static IEnumerable<QuerySpec> GetQueries(GeneratedCorpus corpus) {
    long Hits(string marker) => corpus.Marker(marker).HitCount;
    var unique0 = CodeBaseGenerator.UniqueMarkerPrefix + "00";

    // Code search: planted markers (exact expected counts, so no max results limit).
    const int noLimit = 10_000_000;
    yield return new("unique marker", "code", ServerProtocol.SearchCode(unique0, maxResults: noLimit, matchCase: true), Hits(unique0));
    yield return new("rare marker", "code", ServerProtocol.SearchCode(CodeBaseGenerator.RareMarker, maxResults: noLimit, matchCase: true), Hits(CodeBaseGenerator.RareMarker));
    yield return new("common marker", "code", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker, maxResults: noLimit, matchCase: true), Hits(CodeBaseGenerator.CommonMarker));
    yield return new("common marker, ignore case", "code", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker.ToLowerInvariant(), maxResults: noLimit), Hits(CodeBaseGenerator.CommonMarker));
    yield return new("common marker, whole word", "code", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker, maxResults: noLimit, matchCase: true, matchWholeWord: true), Hits(CodeBaseGenerator.CommonMarker));
    yield return new("large file marker", "code", ServerProtocol.SearchCode(CodeBaseGenerator.LargeFileMarker, maxResults: noLimit, matchCase: true), Hits(CodeBaseGenerator.LargeFileMarker));
    yield return new("regex on unique markers", "code", ServerProtocol.SearchCode(CodeBaseGenerator.UniqueMarkerPrefix + "0[0-4]", maxResults: noLimit, matchCase: true, regex: true), 5);
    yield return new("common marker in *.py", "code", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker, matchCase: true, filePathPattern: "*.py"), null);

    // Code search: real-world-like queries (timing only).
    yield return new("browser (max results)", "code", ServerProtocol.SearchCode("browser"), null);
    yield return new("WebContents", "code", ServerProtocol.SearchCode("WebContents", matchCase: true), null);
    yield return new("std::unique_ptr", "code", ServerProtocol.SearchCode("std::unique_ptr", matchCase: true), null);
    yield return new("no match", "code", ServerProtocol.SearchCode("this text does not exist anywhere 9876", matchCase: true), 0);
    yield return new(@"regex void \w+::\w+\(", "code", ServerProtocol.SearchCode(@"void \w+::\w+\(", matchCase: true, regex: true), null);
    yield return new(@"regex k[A-Z]\w+ = \d+", "code", ServerProtocol.SearchCode(@"k[A-Z]\w+ = \d+", matchCase: true, regex: true), null);

    // File path search.
    yield return new("*.mojom", "path", ServerProtocol.SearchFilePaths("*.mojom"), null);
    yield return new("*_unittest.cc", "path", ServerProtocol.SearchFilePaths("*_unittest.cc"), null);
    yield return new("browser", "path", ServerProtocol.SearchFilePaths("browser"), null);
    yield return new("BUILD.gn", "path", ServerProtocol.SearchFilePaths("BUILD.gn"), null);
    // File path search over all the files (no early stop at max results). Patterns without a
    // directory separator match the file name only; patterns with a separator (or a regex with
    // "/") match the relative path of each file, which the server builds for every file.
    yield return new("*.zzz (name, no match)", "path", ServerProtocol.SearchFilePaths("*.zzz", maxResults: noLimit), 0);
    yield return new("zzz/*.cc (path, no match)", "path", ServerProtocol.SearchFilePaths("zzz/*.cc", maxResults: noLimit), 0);
    yield return new("content/*_unittest.cc (path)", "path", ServerProtocol.SearchFilePaths("content/*_unittest.cc", maxResults: noLimit), null);
    yield return new("browser/**/*.h (path)", "path", ServerProtocol.SearchFilePaths("browser/**/*.h", maxResults: noLimit), null);
    yield return new(@"regex browser/\w+_test (path)", "path", ServerProtocol.SearchFilePaths(@"browser/\w+_test", maxResults: noLimit, regex: true), null);
    yield return new("common marker in zzz/*.cc", "code", ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker, maxResults: noLimit, matchCase: true, filePathPattern: "zzz/*.cc"), 0);
  }

  private async Task<QueryResult> RunQueryAsync(JsonRpcServerClient server, ProcessSampler sampler, QuerySpec query, int runs) {
    var warmup = await server.RequestAsync(query.Request);
    var requestName = "Request:" + query.Request.Method;
    var before = await GetStatisticsAsync(server, forceGc: false);
    var cpuBefore = sampler.TakeSample();
    var timings = new List<double>();
    TimedResponse last = warmup;
    for (var i = 0; i < runs; i++) {
      last = await server.RequestAsync(query.Request);
      timings.Add(last.Elapsed.TotalMilliseconds);
    }
    var cpuAfter = sampler.TakeSample();
    var after = await GetStatisticsAsync(server, forceGc: false);
    var hits = last.Data["hitCount"]?.GetValue<long>() ?? -1;
    return new QueryResult(query.Name, query.Kind, runs,
      ClientP50Ms: Percentile(timings, 50), ClientP95Ms: Percentile(timings, 95), ClientMaxMs: timings.Max(),
      ServerAvgMs: ServerAverageMs(before, after, requestName),
      ServerCpuMsPerRun: (cpuAfter.CpuMs - cpuBefore.CpuMs) / runs,
      ResponseBytes: last.ResponseSizeBytes,
      HitCount: hits, ExpectedHitCount: query.ExpectedHits,
      Correct: query.ExpectedHits is not { } expected || expected == hits);
  }

  private async Task<QueryResult> RunExtractsAsync(JsonRpcServerClient server, ProcessSampler sampler, int runs) {
    var search = await server.RequestAsync(ServerProtocol.SearchCode(CodeBaseGenerator.CommonMarker, maxResults: 10_000_000, matchCase: true));
    var files = ServerProtocol.FlattenCodeResults(search.Data).Take(200).ToList();
    var before = await GetStatisticsAsync(server, forceGc: false);
    var cpuBefore = sampler.TakeSample();
    var timings = new List<double>();
    long extracts = 0;
    var bytes = 0;
    for (var i = 0; i < runs; i++) {
      foreach (var file in files) {
        var response = await server.RequestAsync(ServerProtocol.GetFileExtracts(file.FullPath, file.Positions));
        timings.Add(response.Elapsed.TotalMilliseconds);
        extracts += response.Data["extracts"]?.AsArray().Count ?? 0;
        bytes += response.ResponseSizeBytes;
      }
    }
    var cpuAfter = sampler.TakeSample();
    var after = await GetStatisticsAsync(server, forceGc: false);
    var expected = files.Sum(f => f.Positions.Count) * (long)runs;
    return new QueryResult($"extracts of {files.Count} files", "extracts", timings.Count,
      ClientP50Ms: Percentile(timings, 50), ClientP95Ms: Percentile(timings, 95), ClientMaxMs: timings.DefaultIfEmpty(0).Max(),
      ServerAvgMs: ServerAverageMs(before, after, "Request:files/extracts"),
      ServerCpuMsPerRun: timings.Count > 0 ? (cpuAfter.CpuMs - cpuBefore.CpuMs) / timings.Count : 0,
      ResponseBytes: timings.Count > 0 ? bytes / timings.Count : 0,
      HitCount: extracts, ExpectedHitCount: expected, Correct: extracts == expected);
  }

  /// <summary>
  /// Incremental updates of the index, each repeated <paramref name="runs"/> times:
  /// <list type="bullet">
  /// <item>"modify 1 file": a marker is appended to an existing file, then the file is restored;</item>
  /// <item>"create 1 directory": a directory containing a file with a marker is created at the root of
  /// the corpus, then deleted.</item>
  /// </list>
  /// For each change, the client time is the delay until a search finds (or no longer finds) the
  /// marker, which includes the debouncing of file change notifications; the server times are the
  /// durations of the file system scan and of the file database update measured by the server. The
  /// corpus is always restored (contents and time stamps), so that it stays pristine.
  /// </summary>
  private async Task<List<QueryResult>> RunUpdatesAsync(JsonRpcServerClient server, GeneratedCorpus corpus, int runs) {
    var unique0 = CodeBaseGenerator.UniqueMarkerPrefix + "00";
    var search = await server.RequestAsync(ServerProtocol.SearchCode(unique0, maxResults: 10, matchCase: true));
    var targetFile = ServerProtocol.FlattenCodeResults(search.Data).First().FullPath;
    var originalBytes = await File.ReadAllBytesAsync(targetFile);
    var originalTime = File.GetLastWriteTimeUtc(targetFile);
    var tempDirectory = Path.Combine(corpus.RootPath, "vs-chromium-benchmark-tmp");

    var results = new List<QueryResult>();
    var modify = new UpdateTimings();
    var create = new UpdateTimings();
    try {
      for (var i = 0; i < runs; i++) {
        var marker = $"VsChromiumBenchmarkUpdateMarker{i}x{Environment.TickCount64}";
        var targetName = Path.GetFileName(targetFile);
        await MeasureUpdateAsync(server, modify, marker, targetName, expectedHits: 1,
          () => File.AppendAllText(targetFile, "\n// " + marker + "\n"));
        await MeasureUpdateAsync(server, modify, marker, targetName, expectedHits: 0, () => {
          File.WriteAllBytes(targetFile, originalBytes);
          File.SetLastWriteTimeUtc(targetFile, originalTime);
        });

        marker = $"VsChromiumBenchmarkCreateMarker{i}x{Environment.TickCount64}";
        await MeasureUpdateAsync(server, create, marker, "new_file.cc", expectedHits: 1, () => {
          Directory.CreateDirectory(tempDirectory);
          File.WriteAllText(Path.Combine(tempDirectory, "new_file.cc"), "// " + marker + "\n");
        });
        await MeasureUpdateAsync(server, create, marker, "new_file.cc", expectedHits: 0, () => Directory.Delete(tempDirectory, recursive: true));
      }
    }
    finally {
      File.WriteAllBytes(targetFile, originalBytes);
      File.SetLastWriteTimeUtc(targetFile, originalTime);
      if (Directory.Exists(tempDirectory)) {
        Directory.Delete(tempDirectory, recursive: true);
      }
    }

    results.Add(modify.ToResult("modify 1 file (and restore)"));
    results.Add(create.ToResult("create 1 directory (and delete)"));
    return results;
  }

  private sealed class UpdateTimings {
    public readonly List<double> ClientMs = new();
    public double ScanMs;
    public double UpdateMs;
    public int Count;
    public bool Correct = true;

    public QueryResult ToResult(string name) =>
      new(name, "update", ClientMs.Count,
        ClientP50Ms: Percentile(ClientMs, 50), ClientP95Ms: Percentile(ClientMs, 95), ClientMaxMs: ClientMs.DefaultIfEmpty(0).Max(),
        // Server: file system scan + file database update, per change.
        ServerAvgMs: Count > 0 ? (ScanMs + UpdateMs) / Count : -1,
        ServerCpuMsPerRun: 0,
        ResponseBytes: 0,
        HitCount: Count, ExpectedHitCount: null, Correct: Correct);
  }

  private static async Task MeasureUpdateAsync(JsonRpcServerClient server, UpdateTimings timings, string marker, string fileName, int expectedHits, Action change) {
    var before = await GetStatisticsAsync(server, forceGc: false);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    change();
    var found = false;
    while (stopwatch.Elapsed < TimeSpan.FromSeconds(60)) {
      // Note: Only the file name is searched, so that polling doesn't compete with the update for CPU.
      var response = await server.RequestAsync(ServerProtocol.SearchCode(marker, maxResults: 10, matchCase: true, filePathPattern: fileName));
      if ((response.Data["hitCount"]?.GetValue<long>() ?? -1) == expectedHits) {
        found = true;
        break;
      }
      await Task.Delay(10);
    }
    stopwatch.Stop();
    // Let the server finish the update (the search may see the new contents before the end of it).
    await Task.Delay(200);
    var after = await GetStatisticsAsync(server, forceGc: false);
    timings.ClientMs.Add(stopwatch.Elapsed.TotalMilliseconds);
    timings.ScanMs += TotalMsDelta(before, after, "FileSystemScan");
    timings.UpdateMs += TotalMsDelta(before, after, "FileDatabaseUpdate");
    timings.Count++;
    timings.Correct &= found;
  }

  private static double TotalMsDelta(JsonObject before, JsonObject after, string operation) {
    static double Find(JsonObject stats, string name) =>
      stats["operationTimings"]?.AsArray().FirstOrDefault(t => (string?)t!["name"] == name)?["totalMs"]?.GetValue<double>() ?? 0;
    return Find(after, operation) - Find(before, operation);
  }

  /// <summary>
  /// Waits until the server has built its background index structures (operation
  /// "TrigramFiltersBuild" completed), or until it is clear that it doesn't build any (no filter
  /// memory after a few seconds).
  /// </summary>
  private static async Task<JsonObject> WaitForBackgroundIndexAsync(JsonRpcServerClient server) {
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    while (true) {
      var stats = await GetStatisticsAsync(server, forceGc: false);
      var built = stats["operationTimings"]?.AsArray().Any(t => (string?)t!["name"] == "TrigramFiltersBuild") ?? false;
      var filterBytes = stats["trigramFiltersBytes"]?.GetValue<long>() ?? 0;
      if (built || (filterBytes == 0 && stopwatch.Elapsed > TimeSpan.FromSeconds(3)) || stopwatch.Elapsed > TimeSpan.FromMinutes(5)) {
        return stats;
      }
      await Task.Delay(50);
    }
  }

  private static async Task<JsonObject> GetStatisticsAsync(JsonRpcServerClient server, bool forceGc) =>
    (await server.RequestAsync(ServerProtocol.GetServerStatistics(forceGc))).Data;

  /// <summary>Average duration of a request type measured by the server, between two statistics snapshots.</summary>
  private static double ServerAverageMs(JsonObject before, JsonObject after, string operation) {
    static (long Count, double TotalMs) Find(JsonObject stats, string name) {
      var timing = stats["operationTimings"]?.AsArray().FirstOrDefault(t => (string?)t!["name"] == name);
      return timing == null ? (0, 0) : (timing["count"]!.GetValue<long>(), timing["totalMs"]!.GetValue<double>());
    }
    var (countBefore, totalBefore) = Find(before, operation);
    var (countAfter, totalAfter) = Find(after, operation);
    return countAfter > countBefore ? (totalAfter - totalBefore) / (countAfter - countBefore) : -1;
  }

  private static double Percentile(List<double> values, int percentile) {
    if (values.Count == 0) {
      return 0;
    }
    var sorted = values.OrderBy(x => x).ToList();
    var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
    return sorted[Math.Clamp(index, 0, sorted.Count - 1)];
  }

  private void WriteResults(BenchmarkResult result) {
    var dir = TestSettings.BenchmarkResultsDirectory;
    Directory.CreateDirectory(dir);
    var baseName = $"{result.Corpus.Name}-{result.StartedUtc:yyyyMMdd-HHmmss}-{result.Environment.ServerGitCommit}";
    var jsonPath = Path.Combine(dir, baseName + ".json");
    File.WriteAllText(jsonPath, result.ToJson());
    var mdPath = Path.Combine(dir, baseName + ".md");
    File.WriteAllText(mdPath, result.ToMarkdown());
    Log($"Results: {jsonPath}");
    Log(result.ToMarkdown());

    var baselinePath = Environment.GetEnvironmentVariable("VSCHROMIUM_BENCHMARK_BASELINE");
    if (!string.IsNullOrEmpty(baselinePath)) {
      var comparison = BenchmarkResult.Compare(BenchmarkResult.FromJson(File.ReadAllText(baselinePath)), result);
      File.WriteAllText(Path.Combine(dir, baseName + ".comparison.md"), comparison);
      Log(comparison);
    }
  }

  private void Log(string message) => TestContext.WriteLine(message);
}
