// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Diagnostics;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestProjectOperations {
    [TestMethod]
    public void DurationAccumulatorKeepsCountMinMaxAverageAndLast() {
      var accumulator = new DurationAccumulator();
      Assert.AreEqual(0, accumulator.AverageMs);
      foreach (var ms in new[] { 30.0, 10.0, 50.0, 20.0 }) {
        accumulator.Add(ms);
      }
      Assert.AreEqual(4, accumulator.Count);
      Assert.AreEqual(10, accumulator.MinMs);
      Assert.AreEqual(50, accumulator.MaxMs);
      Assert.AreEqual(27.5, accumulator.AverageMs);
      Assert.AreEqual(20, accumulator.LastMs);
    }

    [TestMethod]
    public void RecordingDoesNotAllocate() {
      var accumulator = new DurationAccumulator();
      accumulator.Add(1);
      var before = GC.GetAllocatedBytesForCurrentThread();
      for (var i = 0; i < 1000; i++) {
        accumulator.Add(i);
      }
      Assert.AreEqual(before, GC.GetAllocatedBytesForCurrentThread());
      Assert.AreEqual(1001, accumulator.Count);
    }
  }
}
