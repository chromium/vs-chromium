// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VsChromium.Core.Files;
using VsChromium.Server.FileSystem;
using VsChromium.Server.FileSystemDatabase;

namespace VsChromium.Server.Diagnostics {
  /// <summary>Count, min, max, sum and last value of durations (no allocation).</summary>
  public struct DurationAccumulator {
    public long Count;
    public double MinMs;
    public double MaxMs;
    public double TotalMs;
    public double LastMs;

    public double AverageMs => Count == 0 ? 0 : TotalMs / Count;

    public void Add(double ms) {
      MinMs = Count == 0 ? ms : Math.Min(MinMs, ms);
      MaxMs = Count == 0 ? ms : Math.Max(MaxMs, ms);
      Count++;
      TotalMs += ms;
      LastMs = ms;
    }
  }

  /// <summary>The operation statistics of a project (see <see cref="ProjectOperations"/>).</summary>
  public struct ProjectOperationStatistics {
    /// <summary>The initial loads of the project (first load, rebuilds): scan and load of its files.</summary>
    public DurationAccumulator InitialLoad;
    /// <summary>The scan of the project during its last initial load.</summary>
    public double LastScanMs;
    /// <summary>The last initial load published the index of the cache first, then verified it.</summary>
    public bool LastFromCache;
    /// <summary>The time to the published index of the cache of the last initial load (if <see cref="LastFromCache"/>).</summary>
    public double LastCachedIndexMs;
    /// <summary>The incremental updates of the project (file system changes, edited files).</summary>
    public DurationAccumulator IncrementalUpdates;
    /// <summary>
    /// The last verification of the tree of the index cache: from the change journal or by a full
    /// scan, and why (null if none).
    /// </summary>
    public ProjectVerification LastVerification;
  }

  /// <summary>
  /// The duration of the high level operations of each project (by project root path): initial loads
  /// and incremental updates. Fixed size state per project, no allocation per operation (except the
  /// first operation of a project). Thread safe.
  /// <para>
  /// Initial load: from the start of the scan of the project to its complete index. The scan time of
  /// each project is exact (the projects are scanned one after the other); the time of the load of
  /// the files is shared by the projects loaded together, in proportion of their number of files with
  /// contents (an approximation of the files read: the same for a cold load). A load from the index
  /// cache (phase 2) records the time to the published index of the cache, and its total time is the
  /// time to the verified index (the verification scans the project and reads the changed files).
  /// </para>
  /// <para>
  /// Incremental updates: each update of the index (file system changes, edited files) counts for
  /// each project it touches, with the duration of the whole update.
  /// </para>
  /// </summary>
  public static class ProjectOperations {
    private static readonly object Lock = new object();
    private static readonly Dictionary<FullPath, Entry> Entries = new Dictionary<FullPath, Entry>();

    private sealed class Entry {
      public ProjectOperationStatistics Statistics;
      /// <summary>An initial load is in progress: its start (<see cref="Stopwatch.GetTimestamp"/>).</summary>
      public bool Pending;
      public long PendingStart;
      public double PendingScanMs;
      public bool PendingFromCache;
      public double PendingCachedIndexMs;
    }

    private static Entry GetEntry(FullPath root) {
      if (!Entries.TryGetValue(root, out var entry)) {
        entry = new Entry();
        Entries.Add(root, entry);
      }
      return entry;
    }

    /// <summary>
    /// A full scan of a project (started at <paramref name="start"/>, a <see
    /// cref="Stopwatch.GetTimestamp"/>): starts an initial load of the project, unless one is in
    /// progress (e.g. the verification of the index of the cache).
    /// </summary>
    public static void RecordFullScan(FullPath root, long start, double scanMs) {
      lock (Lock) {
        var entry = GetEntry(root);
        if (!entry.Pending) {
          entry.Pending = true;
          entry.PendingStart = start;
          entry.PendingFromCache = false;
          entry.PendingCachedIndexMs = 0;
        }
        entry.PendingScanMs = scanMs;
      }
    }

    /// <summary>The verification of the tree of the index cache of a project (not modified after this call).</summary>
    public static void RecordVerification(FullPath root, ProjectVerification verification) {
      lock (Lock) {
        GetEntry(root).Statistics.LastVerification = verification;
      }
    }

    /// <summary>The tree of a project comes from the index cache (phase 2): an initial load from the cache starts.</summary>
    public static void RecordCachedTree(FullPath root, long start) {
      lock (Lock) {
        var entry = GetEntry(root);
        entry.Pending = true;
        entry.PendingStart = start;
        entry.PendingScanMs = 0;
        entry.PendingFromCache = true;
        entry.PendingCachedIndexMs = 0;
      }
    }

    /// <summary>
    /// An update of the index completed (the complete database of <paramref name="snapshot"/>), in
    /// <paramref name="updateMs"/>: completes the initial loads in progress of the projects of the
    /// snapshot, and records an incremental update for the other projects of
    /// <paramref name="touchedProjects"/> (null: a full update, not an incremental update).
    /// </summary>
    public static void RecordUpdate(FileSystemSnapshot snapshot, IFileDatabaseSnapshot database, double updateMs,
                                    IReadOnlyCollection<FullPath> touchedProjects) {
      var now = Stopwatch.GetTimestamp();
      lock (Lock) {
        List<(FullPath Root, Entry Entry)> completed = null;
        foreach (var projectRoot in snapshot.ProjectRoots) {
          var root = projectRoot.Project.RootPath;
          var entry = GetEntry(root);
          if (entry.Pending) {
            if (snapshot.IsCachedProject(root)) {
              // The index of the cache is published: verified later.
              if (entry.PendingCachedIndexMs == 0) {
                entry.PendingCachedIndexMs = Stopwatch.GetElapsedTime(entry.PendingStart, now).TotalMilliseconds;
              }
            } else {
              (completed ??= new List<(FullPath, Entry)>()).Add((root, entry));
            }
          } else if (touchedProjects != null && touchedProjects.Contains(root)) {
            entry.Statistics.IncrementalUpdates.Add(updateMs);
          }
        }
        if (completed == null)
          return;

        var shares = GetLoadShares(database, completed.Select(x => x.Root).ToList());
        foreach (var (root, entry) in completed) {
          var totalMs = entry.PendingFromCache
            ? Stopwatch.GetElapsedTime(entry.PendingStart, now).TotalMilliseconds
            : entry.PendingScanMs + updateMs * shares[root];
          entry.Statistics.InitialLoad.Add(totalMs);
          entry.Statistics.LastScanMs = entry.PendingScanMs;
          entry.Statistics.LastFromCache = entry.PendingFromCache;
          entry.Statistics.LastCachedIndexMs = entry.PendingCachedIndexMs;
          entry.Pending = false;
        }
      }
    }

    /// <summary>
    /// The share of the load of each project of <paramref name="roots"/>: its files with contents
    /// (equal shares if unknown).
    /// </summary>
    private static Dictionary<FullPath, double> GetLoadShares(IFileDatabaseSnapshot database, List<FullPath> roots) {
      var result = roots.ToDictionary(x => x, _ => 1.0 / roots.Count);
      if (roots.Count == 1 || database is not FileDatabaseSnapshot snapshot)
        return result;
      var ranges = FileContentsStatistics.GetProjectFileRanges(snapshot);
      var counts = roots.ToDictionary(x => x, x => ranges.TryGetValue(x, out var projectRanges)
        ? FileContentsStatistics.GetContents(snapshot, projectRanges).LongCount(c => c != null)
        : 0);
      var total = counts.Values.Sum();
      if (total > 0) {
        foreach (var root in roots) {
          result[root] = (double)counts[root] / total;
        }
      }
      return result;
    }

    /// <summary>The statistics of a project (empty if it has none).</summary>
    public static ProjectOperationStatistics GetStatistics(FullPath root) {
      lock (Lock) {
        return Entries.TryGetValue(root, out var entry) ? entry.Statistics : default;
      }
    }
  }
}
