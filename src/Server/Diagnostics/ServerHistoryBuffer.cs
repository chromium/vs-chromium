// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;

namespace VsChromium.Server.Diagnostics {
  /// <summary>
  /// A sample of the history of the server (see <see cref="ServerHistoryBuffer"/>): cheap metrics only.
  /// </summary>
  public struct ServerHistorySample {
    /// <summary>Milliseconds since the Unix epoch (UTC).</summary>
    public long TimeUnixMs;
    public long GcHeapBytes;
    public long GcTotalAllocatedBytes;
    public long NativeAllocatedBytes;
    public long WorkingSetBytes;
    public long PrivateBytes;
    public double CpuTotalMs;
    public int FileCount;
    public int SearchableFileCount;
    public long IndexedTextBytes;
    public long CacheSizeOnDiskBytes;
    public IndexingServerStatus Status;
  }

  /// <summary>
  /// The samples of a <see cref="ServerHistoryBuffer"/>, oldest first, one array per metric (columns).
  /// </summary>
  public sealed class ServerHistorySamples {
    public long[] TimeUnixMs;
    public long[] GcHeapBytes;
    public long[] GcTotalAllocatedBytes;
    public long[] NativeAllocatedBytes;
    public long[] WorkingSetBytes;
    public long[] PrivateBytes;
    public double[] CpuTotalMs;
    public int[] FileCount;
    public int[] SearchableFileCount;
    public long[] IndexedTextBytes;
    public long[] CacheSizeOnDiskBytes;
    public IndexingServerStatus[] Status;

    public int Count => TimeUnixMs.Length;
  }

  /// <summary>
  /// The history of the server: the last <see cref="Capacity"/> samples (ring buffer, one array per
  /// metric, allocated once: adding a sample doesn't allocate). Thread safe.
  /// </summary>
  public sealed class ServerHistoryBuffer {
    /// <summary>The default number of samples: 2 hours of samples every 10 seconds.</summary>
    public const int DefaultCapacity = 720;

    private readonly object _lock = new object();
    private readonly long[] _timeUnixMs;
    private readonly long[] _gcHeapBytes;
    private readonly long[] _gcTotalAllocatedBytes;
    private readonly long[] _nativeAllocatedBytes;
    private readonly long[] _workingSetBytes;
    private readonly long[] _privateBytes;
    private readonly double[] _cpuTotalMs;
    private readonly int[] _fileCount;
    private readonly int[] _searchableFileCount;
    private readonly long[] _indexedTextBytes;
    private readonly long[] _cacheSizeOnDiskBytes;
    private readonly IndexingServerStatus[] _status;
    /// <summary>The slot of the next sample.</summary>
    private int _next;
    private int _count;

    public ServerHistoryBuffer(int capacity = DefaultCapacity) {
      if (capacity <= 0)
        throw new ArgumentOutOfRangeException(nameof(capacity));
      Capacity = capacity;
      _timeUnixMs = new long[capacity];
      _gcHeapBytes = new long[capacity];
      _gcTotalAllocatedBytes = new long[capacity];
      _nativeAllocatedBytes = new long[capacity];
      _workingSetBytes = new long[capacity];
      _privateBytes = new long[capacity];
      _cpuTotalMs = new double[capacity];
      _fileCount = new int[capacity];
      _searchableFileCount = new int[capacity];
      _indexedTextBytes = new long[capacity];
      _cacheSizeOnDiskBytes = new long[capacity];
      _status = new IndexingServerStatus[capacity];
    }

    public int Capacity { get; }

    public int Count {
      get {
        lock (_lock) {
          return _count;
        }
      }
    }

    /// <summary>Adds a sample (replaces the oldest one if the history is full). No allocation.</summary>
    public void Add(in ServerHistorySample sample) {
      lock (_lock) {
        var i = _next;
        _timeUnixMs[i] = sample.TimeUnixMs;
        _gcHeapBytes[i] = sample.GcHeapBytes;
        _gcTotalAllocatedBytes[i] = sample.GcTotalAllocatedBytes;
        _nativeAllocatedBytes[i] = sample.NativeAllocatedBytes;
        _workingSetBytes[i] = sample.WorkingSetBytes;
        _privateBytes[i] = sample.PrivateBytes;
        _cpuTotalMs[i] = sample.CpuTotalMs;
        _fileCount[i] = sample.FileCount;
        _searchableFileCount[i] = sample.SearchableFileCount;
        _indexedTextBytes[i] = sample.IndexedTextBytes;
        _cacheSizeOnDiskBytes[i] = sample.CacheSizeOnDiskBytes;
        _status[i] = sample.Status;
        _next = (i + 1) % Capacity;
        _count = Math.Min(_count + 1, Capacity);
      }
    }

    /// <summary>
    /// The samples taken after <paramref name="sinceUnixMs"/> (all the samples if null), oldest
    /// first.
    /// </summary>
    public ServerHistorySamples GetSamples(long? sinceUnixMs = null) {
      lock (_lock) {
        var oldest = (_next - _count + Capacity) % Capacity;
        // The samples are in time order: skip the ones not after "since".
        var skip = 0;
        if (sinceUnixMs.HasValue) {
          while (skip < _count && _timeUnixMs[(oldest + skip) % Capacity] <= sinceUnixMs.Value) {
            skip++;
          }
        }
        var first = (oldest + skip) % Capacity;
        var count = _count - skip;
        return new ServerHistorySamples {
          TimeUnixMs = Copy(_timeUnixMs, first, count),
          GcHeapBytes = Copy(_gcHeapBytes, first, count),
          GcTotalAllocatedBytes = Copy(_gcTotalAllocatedBytes, first, count),
          NativeAllocatedBytes = Copy(_nativeAllocatedBytes, first, count),
          WorkingSetBytes = Copy(_workingSetBytes, first, count),
          PrivateBytes = Copy(_privateBytes, first, count),
          CpuTotalMs = Copy(_cpuTotalMs, first, count),
          FileCount = Copy(_fileCount, first, count),
          SearchableFileCount = Copy(_searchableFileCount, first, count),
          IndexedTextBytes = Copy(_indexedTextBytes, first, count),
          CacheSizeOnDiskBytes = Copy(_cacheSizeOnDiskBytes, first, count),
          Status = Copy(_status, first, count),
        };
      }
    }

    private T[] Copy<T>(T[] ring, int first, int count) {
      var result = new T[count];
      var head = Math.Min(count, Capacity - first);
      Array.Copy(ring, first, result, 0, head);
      Array.Copy(ring, 0, result, head, count - head);
      return result;
    }
  }
}
