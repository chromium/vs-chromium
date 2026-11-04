// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using VsChromium.Core.Logging;
using VsChromium.Core.Memory;
using VsChromium.Server.Cache;
using VsChromium.Server.Search;

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// Takes a sample of the server state every <see cref="Interval"/> (10 seconds) into a <see
  /// cref="ServerHistoryBuffer"/> ("server/history"): cheap metrics only, values already known by the
  /// server (counters, precomputed counts of the current database, state of the index cache in
  /// memory). No walk of the index, no disk access.
  /// </summary>
  public sealed class ServerHistorySampler : IDisposable {
    /// <summary>Test only: the interval of the samples, in milliseconds.</summary>
    public const string IntervalVariable = "VSCHROMIUM_TEST_HISTORY_INTERVAL_MS";

    public static readonly TimeSpan DefaultInterval = TimeSpan.FromSeconds(10);

    private readonly ISearchEngine _searchEngine;
    private readonly IIndexingServer _indexingServer;
    private readonly IIndexCache _indexCache;
    private readonly object _lock = new object();
    /// <summary>The process of the server (refreshed by each sample: working set, private bytes).</summary>
    private readonly Process _process = Process.GetCurrentProcess();
    private Timer _timer;
    private bool _disposed;

    public ServerHistorySampler(ISearchEngine searchEngine, IIndexingServer indexingServer, IIndexCache indexCache) {
      _searchEngine = searchEngine;
      _indexingServer = indexingServer;
      _indexCache = indexCache;
      Interval = int.TryParse(Environment.GetEnvironmentVariable(IntervalVariable), out var ms) && ms > 0
        ? TimeSpan.FromMilliseconds(ms)
        : DefaultInterval;
    }

    public ServerHistoryBuffer History { get; } = new ServerHistoryBuffer();

    public TimeSpan Interval { get; }

    /// <summary>Starts sampling: a first sample now, then every <see cref="Interval"/>.</summary>
    public void Start() {
      lock (_lock) {
        if (_disposed || _timer != null)
          return;
        _timer = new Timer(_ => AddSample(), null, TimeSpan.Zero, Interval);
      }
    }

    private void AddSample() {
      try {
        lock (_lock) {
          if (_disposed)
            return;
          History.Add(TakeSample());
        }
      }
      catch (Exception e) {
        Logger.LogWarn(e, "Error sampling the state of the server.");
      }
    }

    /// <summary>The current state of the server.</summary>
    public ServerHistorySample TakeSample() {
      var database = _searchEngine.CurrentFileDatabaseSnapshot;
      _process.Refresh();
      return new ServerHistorySample {
        TimeUnixMs = (DateTime.UtcNow.Ticks - DateTime.UnixEpoch.Ticks) / TimeSpan.TicksPerMillisecond,
        GcHeapBytes = GC.GetTotalMemory(false),
        GcTotalAllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
        NativeAllocatedBytes = NativeHeap.TotalMemory,
        WorkingSetBytes = _process.WorkingSet64,
        PrivateBytes = _process.PrivateMemorySize64,
        CpuTotalMs = Environment.CpuUsage.TotalTime.TotalMilliseconds,
        // Note: Computed when the database is activated (see IFileDatabaseSnapshot.PrecomputeSearchData).
        FileCount = (int)Math.Min(database.FileNameCount, int.MaxValue),
        SearchableFileCount = (int)Math.Min(database.SearchableFileCount, int.MaxValue),
        IndexedTextBytes = database.TotalFileContentsLength,
        CacheSizeOnDiskBytes = _indexCache?.GetDiskState().SizeOnDiskBytes ?? 0,
        Status = _indexingServer.CurrentState.Status,
      };
    }

    public void Dispose() {
      lock (_lock) {
        _disposed = true;
        _timer?.Dispose();
        _process.Dispose();
      }
    }
  }
}
