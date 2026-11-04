// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server;
using VsChromium.Server.Diagnostics;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerHistory {
    private static ServerHistorySample Sample(long time) {
      return new ServerHistorySample {
        TimeUnixMs = time,
        GcHeapBytes = time * 10,
        GcTotalAllocatedBytes = time * 20,
        NativeAllocatedBytes = time * 30,
        WorkingSetBytes = time * 40,
        PrivateBytes = time * 50,
        CpuTotalMs = time / 2.0,
        FileCount = (int)time + 1,
        SearchableFileCount = (int)time + 2,
        IndexedTextBytes = time * 60,
        CacheSizeOnDiskBytes = time * 70,
        Status = time % 2 == 0 ? IndexingServerStatus.Idle : IndexingServerStatus.Busy,
      };
    }

    [TestMethod]
    public void SamplesAreReturnedOldestFirstInColumns() {
      var history = new ServerHistoryBuffer(10);
      for (var i = 1; i <= 3; i++) {
        history.Add(Sample(i));
      }
      var samples = history.GetSamples();
      Assert.AreEqual(3, samples.Count);
      CollectionAssert.AreEqual(new long[] { 1, 2, 3 }, samples.TimeUnixMs);
      CollectionAssert.AreEqual(new long[] { 10, 20, 30 }, samples.GcHeapBytes);
      CollectionAssert.AreEqual(new long[] { 20, 40, 60 }, samples.GcTotalAllocatedBytes);
      CollectionAssert.AreEqual(new long[] { 30, 60, 90 }, samples.NativeAllocatedBytes);
      CollectionAssert.AreEqual(new long[] { 40, 80, 120 }, samples.WorkingSetBytes);
      CollectionAssert.AreEqual(new long[] { 50, 100, 150 }, samples.PrivateBytes);
      CollectionAssert.AreEqual(new[] { 0.5, 1, 1.5 }, samples.CpuTotalMs);
      CollectionAssert.AreEqual(new[] { 2, 3, 4 }, samples.FileCount);
      CollectionAssert.AreEqual(new[] { 3, 4, 5 }, samples.SearchableFileCount);
      CollectionAssert.AreEqual(new long[] { 60, 120, 180 }, samples.IndexedTextBytes);
      CollectionAssert.AreEqual(new long[] { 70, 140, 210 }, samples.CacheSizeOnDiskBytes);
      CollectionAssert.AreEqual(new[] { IndexingServerStatus.Busy, IndexingServerStatus.Idle, IndexingServerStatus.Busy }, samples.Status);
    }

    [TestMethod]
    public void TheHistoryKeepsTheLastSamples() {
      var history = new ServerHistoryBuffer(10);
      for (var i = 1; i <= 25; i++) {
        history.Add(Sample(i));
        Assert.AreEqual(Math.Min(i, 10), history.Count);
      }
      var samples = history.GetSamples();
      CollectionAssert.AreEqual(Enumerable.Range(16, 10).Select(x => (long)x).ToArray(), samples.TimeUnixMs);
      CollectionAssert.AreEqual(Enumerable.Range(16, 10).Select(x => x + 1).ToArray(), samples.FileCount);
    }

    [TestMethod]
    public void SinceReturnsTheSamplesAfterATime() {
      var history = new ServerHistoryBuffer(10);
      for (var i = 1; i <= 15; i++) {
        history.Add(Sample(i * 100));
      }
      // Samples 600..1500 are kept.
      CollectionAssert.AreEqual(new long[] { 1300, 1400, 1500 }, history.GetSamples(1200).TimeUnixMs);
      CollectionAssert.AreEqual(new long[] { 1300, 1400, 1500 }, history.GetSamples(1250).TimeUnixMs);
      CollectionAssert.AreEqual(new long[] { 1300, 1400, 1500 }, history.GetSamples(1250).CacheSizeOnDiskBytes.Select(x => x / 70).ToArray());
      Assert.AreEqual(0, history.GetSamples(1500).Count, "Nothing new");
      Assert.AreEqual(10, history.GetSamples(0).Count, "All");
      Assert.AreEqual(10, history.GetSamples(null).Count, "All");
      Assert.AreEqual(0, new ServerHistoryBuffer(5).GetSamples().Count, "Empty");
    }

    [TestMethod]
    public void AddingSamplesDoesNotAllocate() {
      var history = new ServerHistoryBuffer();
      Assert.AreEqual(ServerHistoryBuffer.DefaultCapacity, history.Capacity);
      history.Add(Sample(0));
      var before = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 1; i <= 3 * ServerHistoryBuffer.DefaultCapacity; i++) {
        history.Add(Sample(i));
      }
      Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
      Assert.AreEqual(ServerHistoryBuffer.DefaultCapacity, history.Count);
    }

    [TestMethod]
    public void SamplesOfTheServerAreCheap() {
      using (var components = new ServerComponents()) {
        var sampler = components.ServerHistorySampler;
        Assert.AreEqual(ServerHistorySampler.DefaultInterval, sampler.Interval);
        sampler.TakeSample();
        const int count = 20;
        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        ServerHistorySample sample = default;
        for (var i = 0; i < count; i++) {
          sample = sampler.TakeSample();
        }
        var perSampleMs = stopwatch.Elapsed.TotalMilliseconds / count;
        var perSampleBytes = (GC.GetAllocatedBytesForCurrentThread() - allocated) / count;
        Console.WriteLine("Sample: {0:n3} ms, {1:n0} bytes allocated", perSampleMs, perSampleBytes);
        Assert.IsTrue(sample.WorkingSetBytes > 0);
        Assert.IsTrue(sample.PrivateBytes > 0);
        Assert.IsTrue(sample.CpuTotalMs > 0);
        Assert.IsTrue(sample.GcHeapBytes > 0);
        Assert.AreEqual(IndexingServerStatus.Idle, sample.Status);
        Assert.AreEqual(0, sample.FileCount);
        // Generous bounds (debug build, busy machines): every 10 seconds.
        Assert.IsTrue(perSampleMs < 50, $"{perSampleMs} ms");
        Assert.IsTrue(perSampleBytes < 64 * 1024, $"{perSampleBytes} bytes");
      }
    }
  }
}
