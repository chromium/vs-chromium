// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Diagnostics;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestOperationTimings {
    private static readonly DateTime Start = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

    private static OperationTiming Get(string name) {
      return OperationTimings.GetSnapshot().Single(x => x.Name == name);
    }

    [TestMethod]
    public void RecentDurationsAreKeptOldestFirst() {
      var name = "Test:" + Guid.NewGuid().ToString("N");
      for (var i = 1; i <= 3; i++) {
        OperationTimings.Record(name, TimeSpan.FromMilliseconds(i), Start.AddSeconds(i));
      }
      var timing = Get(name);
      Assert.AreEqual(3, timing.Count);
      CollectionAssert.AreEqual(new double[] { 1, 2, 3 }, timing.RecentMs);
      var startUnixMs = new DateTimeOffset(Start).ToUnixTimeMilliseconds();
      CollectionAssert.AreEqual(new[] { startUnixMs + 1000, startUnixMs + 2000, startUnixMs + 3000 }, timing.RecentEndUnixMs);
    }

    [TestMethod]
    public void OnlyTheLast20DurationsAreKept() {
      var name = "Test:" + Guid.NewGuid().ToString("N");
      for (var i = 1; i <= 45; i++) {
        OperationTimings.Record(name, TimeSpan.FromMilliseconds(i), Start.AddSeconds(i));
      }
      var timing = Get(name);
      Assert.AreEqual(45, timing.Count);
      Assert.AreEqual(45, timing.LastMs);
      Assert.AreEqual(45, timing.MaxMs);
      Assert.AreEqual(Enumerable.Range(1, 45).Sum(), timing.TotalMs);
      Assert.AreEqual(OperationTimings.RecentCount, timing.RecentMs.Length);
      CollectionAssert.AreEqual(Enumerable.Range(26, 20).Select(x => (double)x).ToArray(), timing.RecentMs);
      var startUnixMs = new DateTimeOffset(Start).ToUnixTimeMilliseconds();
      Assert.AreEqual(startUnixMs + 26_000, timing.RecentEndUnixMs[0]);
      Assert.AreEqual(startUnixMs + 45_000, timing.RecentEndUnixMs[19]);
    }

    [TestMethod]
    public void RecordingDoesNotAllocate() {
      var name = "Test:" + Guid.NewGuid().ToString("N");
      OperationTimings.Record(name, TimeSpan.FromMilliseconds(1), Start);
      var before = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 0; i < 1000; i++) {
        OperationTimings.Record(name, TimeSpan.FromMilliseconds(i), Start.AddMilliseconds(i));
      }
      Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
    }
  }
}
