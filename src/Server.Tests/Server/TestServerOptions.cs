// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server;
using VsChromium.Server.Cache;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestServerOptions {
    [TestMethod]
    public void DefaultsWithoutBenchOptions() {
      var options = ServerOptions.FromCommandLine(new[] { "--jsonrpc", "--no-cache" });
      Assert.IsFalse(options.Bench);
      Assert.IsNull(options.Threads);
      Assert.IsNull(options.WatcherDelay);
      Assert.IsFalse(options.NoJournal);
      Assert.IsFalse(options.IndexCache.IsEnabled);
    }

    [TestMethod]
    public void ParsesTheBenchOptions() {
      var options = ServerOptions.FromCommandLine(new[] {
        "--jsonrpc", "--bench", "--threads", "0", "--watcher-delay-ms", "25", "--no-journal", "--cache-dir", "cache",
      });
      Assert.IsTrue(options.Bench);
      Assert.AreEqual(0, options.Threads);
      Assert.AreEqual(TimeSpan.FromMilliseconds(25), options.WatcherDelay);
      Assert.IsTrue(options.NoJournal);
      Assert.IsTrue(options.IndexCache.IsEnabled);
    }

    [TestMethod]
    public void RejectsInvalidValues() {
      Assert.ThrowsExactly<ArgumentException>(() => ServerOptions.FromCommandLine(new[] { "--jsonrpc", "--threads" }));
      Assert.ThrowsExactly<ArgumentException>(() => ServerOptions.FromCommandLine(new[] { "--jsonrpc", "--threads", "-1" }));
      Assert.ThrowsExactly<ArgumentException>(() => ServerOptions.FromCommandLine(new[] { "--jsonrpc", "--watcher-delay-ms", "soon" }));
    }

    [TestMethod]
    public void SaveNowSavesADirtyIndexAndWaits() {
      var saves = 0;
      using (var scheduler = new IndexCacheSaveScheduler(TimeSpan.FromHours(1), TimeSpan.FromHours(1), _ => {
               Thread.Sleep(50);
               Interlocked.Increment(ref saves);
             })) {
        Assert.IsFalse(scheduler.SaveNow(TimeSpan.FromSeconds(10)), "nothing to save");
        scheduler.NotifyChanged(immediate: false);
        Assert.IsTrue(scheduler.SaveNow(TimeSpan.FromSeconds(10)));
        Assert.AreEqual(1, saves);
        Assert.IsFalse(scheduler.IsDirty);
        Assert.IsFalse(scheduler.SaveNow(TimeSpan.FromSeconds(10)));
        Assert.AreEqual(1, saves);
      }
    }
  }
}
