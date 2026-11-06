// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Diagnostics;
using System.Text.Json.Nodes;
using VsChromium.Bench.Servers;
using VsChromium.Bench.Shared.Server;

namespace VsChromium.Bench.Scenarios;

/// <summary>CPU time and memory of the server process at a point in time.</summary>
public sealed record ProcessSnapshot(TimeSpan Elapsed, TimeSpan UserCpu, TimeSpan KernelCpu, long WorkingSet, long PeakWorkingSet, long PrivateBytes) {
  public static ProcessSnapshot Take(Process process, Stopwatch clock) {
    process.Refresh();
    return new ProcessSnapshot(clock.Elapsed, process.UserProcessorTime, process.PrivilegedProcessorTime,
                               process.WorkingSet64, process.PeakWorkingSet64, process.PrivateMemorySize64);
  }
}

/// <summary>A notification of the server with the time it was received (bench clock).</summary>
public sealed record ReceivedNotification(string Method, JsonObject Params, TimeSpan Time);

/// <summary>
/// A server process driven by the bench: JSON-RPC client, notifications with their arrival time, CPU
/// and memory of the process (measured from outside: works with any server version).
/// </summary>
public sealed class ServerHost : IAsyncDisposable {
  private readonly List<ReceivedNotification> _notifications = [];
  private readonly SemaphoreSlim _notificationSignal = new(0);
  private readonly Stopwatch _clock;

  private ServerHost(JsonRpcServerClient client, Stopwatch clock, ServerCapabilities capabilities) {
    Client = client;
    _clock = clock;
    Capabilities = capabilities;
    client.OnNotification((method, parameters) => {
      lock (_notifications) {
        _notifications.Add(new ReceivedNotification(method, parameters, clock.Elapsed));
      }
      _notificationSignal.Release();
    });
  }

  public JsonRpcServerClient Client { get; }
  public ServerCapabilities Capabilities { get; }
  public Process Process => Client.Process;
  public TimeSpan Now => _clock.Elapsed;

  /// <summary>Starts a server with the index cache directory <paramref name="cacheDirectory"/> (null: no cache).</summary>
  public static async Task<ServerHost> StartAsync(string serverPath, string? cacheDirectory, ServerCapabilities capabilities, Stopwatch clock,
                                                  IReadOnlyList<string>? extraArguments = null) {
    var arguments = new List<string>();
    if (capabilities.Bench) {
      arguments.Add(ServerCapabilities.BenchOption);
    }
    arguments.AddRange(extraArguments ?? []);
    var client = await JsonRpcServerClient.StartAsync(serverPath, new ServerStartOptions {
      CacheDirectory = cacheDirectory,
      NoCache = cacheDirectory == null,
      ExtraArguments = arguments,
      ClientName = "vschromium-bench",
      InitializeTimeout = TimeSpan.FromMinutes(2),
      Environment = new Dictionary<string, string?> {
        // The index cache is saved 1 s after the index settles (instead of 30 s), at most 10 s after
        // the first change: the bench waits for the saves between the measurements.
        ["VSCHROMIUM_TEST_CACHE_SAVE_DELAYS_MS"] = "1000,10000",
      },
    });
    return new ServerHost(client, clock, capabilities.WithInitializeResult(client.InitializeResult));
  }

  public ProcessSnapshot Snapshot() => ProcessSnapshot.Take(Process, _clock);

  /// <summary>The number of notifications received so far (a cursor for <see cref="WaitForNotificationAsync"/>).</summary>
  public int NotificationCount {
    get { lock (_notifications) { return _notifications.Count; } }
  }

  /// <summary>
  /// Waits for the first notification <paramref name="method"/> received after the cursor
  /// <paramref name="from"/> and matching <paramref name="predicate"/>; returns it and the next cursor.
  /// </summary>
  public async Task<(ReceivedNotification Notification, int Next)> WaitForNotificationAsync(
      string method, int from, Func<JsonObject, bool>? predicate, TimeSpan timeout, CancellationToken cancellation) {
    var deadline = _clock.Elapsed + timeout;
    var index = from;
    while (true) {
      lock (_notifications) {
        for (; index < _notifications.Count; index++) {
          var n = _notifications[index];
          if (n.Method == method && (predicate == null || predicate(n.Params))) {
            return (n, index + 1);
          }
        }
      }
      var remaining = deadline - _clock.Elapsed;
      if (remaining <= TimeSpan.Zero || Process.HasExited) {
        throw new TimeoutException($"Notification {method} not received within {timeout} (server exited: {Process.HasExited}).");
      }
      await _notificationSignal.WaitAsync(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), cancellation);
    }
  }

  public async Task<JsonObject> RequestAsync(ServerRequest request, TimeSpan? timeout = null) =>
    (await Client.RequestAsync(request, timeout)).Data;

  public Task<JsonObject> StatisticsAsync() => RequestAsync(ServerProtocol.GetServerStatistics());

  public Task<JsonObject> ProjectDetailsAsync(string projectRoot) => RequestAsync(ServerProtocol.GetProjectDetails(projectRoot));

  /// <summary>Hit count of a case-sensitive search of <paramref name="text"/>.</summary>
  public async Task<long> CountHitsAsync(string text, int maxResults = 1_000_000) {
    var result = await RequestAsync(ServerProtocol.SearchCode(text, maxResults: maxResults, matchCase: true));
    return result["hitCount"]?.GetValue<long>() ?? -1;
  }

  /// <summary>
  /// Waits until the server is idle with its index cache saved: status "idle", the cache neither
  /// dirty nor saving (so that the next measurement isn't disturbed by a save).
  /// </summary>
  public async Task WaitForQuiescenceAsync(TimeSpan timeout, CancellationToken cancellation) {
    var deadline = _clock.Elapsed + timeout;
    while (_clock.Elapsed < deadline) {
      var status = await RequestAsync(ServerProtocol.GetIndexStatus());
      var cache = status["indexCache"] as JsonObject;
      var idle = (string?)status["status"] == "idle";
      var saved = cache == null || !((bool?)cache["enabled"] ?? false) || (!((bool?)cache["dirty"] ?? false) && !((bool?)cache["saving"] ?? false));
      if (idle && saved) {
        return;
      }
      await Task.Delay(250, cancellation);
    }
    throw new TimeoutException($"The server did not become idle with its cache saved within {timeout}.");
  }

  public async ValueTask DisposeAsync() {
    await Client.DisposeAsync();
    _notificationSignal.Dispose();
  }
}

/// <summary>Helpers reading values of the statistics of the server.</summary>
public static class Stats {
  /// <summary>The last duration (ms) of a named operation of "server/statistics" (operationTimings), or null.</summary>
  public static double? OperationLastMs(JsonObject statistics, string name) =>
    Operation(statistics, name)?["lastMs"]?.GetValue<double>();

  public static double OperationTotalMs(JsonObject statistics, string name) =>
    Operation(statistics, name)?["totalMs"]?.GetValue<double>() ?? 0;

  public static long OperationCount(JsonObject statistics, string name) =>
    Operation(statistics, name)?["count"]?.GetValue<long>() ?? 0;

  private static JsonNode? Operation(JsonObject statistics, string name) =>
    statistics["operationTimings"]?.AsArray().FirstOrDefault(t => (string?)t?["name"] == name);

  public static double? Double(JsonNode? node, params string[] path) {
    foreach (var name in path) {
      node = node?[name];
    }
    return node is JsonValue value && value.TryGetValue<double>(out var d) ? d : null;
  }

  public static string? String(JsonNode? node, params string[] path) {
    foreach (var name in path) {
      node = node?[name];
    }
    return node is JsonValue value && value.TryGetValue<string>(out var s) ? s : null;
  }
}
