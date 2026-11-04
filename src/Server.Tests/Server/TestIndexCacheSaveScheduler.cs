// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Cache;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestIndexCacheSaveScheduler {
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan Max = TimeSpan.FromMilliseconds(800);

    [TestMethod]
    public void SaveTimeIsTheSettleDelayAfterTheLastUpdateButAtMostTheMaxDelayAfterTheFirst() {
      var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
      var settle = TimeSpan.FromSeconds(30);
      var max = TimeSpan.FromMinutes(5);
      Assert.AreEqual(first + settle, IndexCacheSaveScheduler.GetSaveTime(first, first, settle, max));
      Assert.AreEqual(first.AddMinutes(1) + settle, IndexCacheSaveScheduler.GetSaveTime(first, first.AddMinutes(1), settle, max));
      Assert.AreEqual(first + max, IndexCacheSaveScheduler.GetSaveTime(first, first.AddMinutes(4.9), settle, max));
    }

    [TestMethod]
    public void SavesOnceTheChangesSettle() {
      var saves = 0;
      using (var scheduler = new IndexCacheSaveScheduler(Settle, TimeSpan.FromSeconds(30), _ => Interlocked.Increment(ref saves))) {
        scheduler.NotifyChanged(immediate: false);
        Assert.IsTrue(scheduler.IsDirty);
        Thread.Sleep(100);
        scheduler.NotifyChanged(immediate: false);
        Thread.Sleep(150);
        Assert.AreEqual(0, Volatile.Read(ref saves), "Not settled yet");
        WaitFor(() => Volatile.Read(ref saves) == 1, "settled save");
        WaitFor(() => !scheduler.IsDirty && !scheduler.IsSaving, "clean");
        Thread.Sleep(500);
        Assert.AreEqual(1, Volatile.Read(ref saves), "No save without change");
      }
    }

    [TestMethod]
    public void ContinuousChangesAreSavedAtTheMaxDelay() {
      var saves = 0;
      using (var scheduler = new IndexCacheSaveScheduler(Settle, Max, _ => Interlocked.Increment(ref saves))) {
        // Changes every 50 ms (never settled) during 2 s.
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < TimeSpan.FromSeconds(2)) {
          scheduler.NotifyChanged(immediate: false);
          Thread.Sleep(50);
        }
        var duringChanges = Volatile.Read(ref saves);
        Assert.IsTrue(duringChanges >= 1 && duringChanges <= 3, $"{duringChanges} saves");
        // Then settled: a last save.
        WaitFor(() => Volatile.Read(ref saves) == duringChanges + 1 && !scheduler.IsDirty, "last save");
      }
    }

    [TestMethod]
    public void ImmediateSave() {
      var saved = new ManualResetEventSlim();
      using (var scheduler = new IndexCacheSaveScheduler(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), _ => saved.Set())) {
        scheduler.NotifyChanged(immediate: true);
        Assert.IsTrue(saved.Wait(TimeSpan.FromSeconds(5)));
      }
    }

    [TestMethod]
    public void ChangesDuringASaveAreSavedAfterIt() {
      var saves = 0;
      var inSave = new ManualResetEventSlim();
      var release = new ManualResetEventSlim();
      using (var scheduler = new IndexCacheSaveScheduler(Settle, Max, _ => {
               if (Interlocked.Increment(ref saves) == 1) {
                 inSave.Set();
                 release.Wait();
               }
             })) {
        scheduler.NotifyChanged(immediate: true);
        Assert.IsTrue(inSave.Wait(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.IsSaving);
        Assert.IsFalse(scheduler.IsDirty, "The save has the last state");
        // A change during the save: dirty again, the save is not cancelled.
        scheduler.NotifyChanged(immediate: false);
        Assert.IsTrue(scheduler.IsDirty);
        release.Set();
        WaitFor(() => Volatile.Read(ref saves) == 2 && !scheduler.IsDirty && !scheduler.IsSaving, "second save");
      }
    }

    [TestMethod]
    public void CancelledSaveStaysDirty() {
      var inSave = new ManualResetEventSlim();
      using (var scheduler = new IndexCacheSaveScheduler(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), token => {
               inSave.Set();
               token.WaitHandle.WaitOne();
               token.ThrowIfCancellationRequested();
             })) {
        scheduler.NotifyChanged(immediate: true);
        Assert.IsTrue(inSave.Wait(TimeSpan.FromSeconds(5)));
        scheduler.CancelSave();
        Assert.IsTrue(scheduler.WaitForSave(TimeSpan.FromSeconds(5)));
        Assert.IsTrue(scheduler.IsDirty);
      }
    }

    [TestMethod]
    public void DisposeDoesNotWaitForTheSave() {
      var inSave = new ManualResetEventSlim();
      var cancelled = new ManualResetEventSlim();
      var scheduler = new IndexCacheSaveScheduler(TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(5), token => {
        inSave.Set();
        // A save that takes a while to notice the cancellation.
        token.WaitHandle.WaitOne();
        cancelled.Set();
        Thread.Sleep(3000);
      });
      scheduler.NotifyChanged(immediate: true);
      Assert.IsTrue(inSave.Wait(TimeSpan.FromSeconds(5)));
      var stopwatch = Stopwatch.StartNew();
      scheduler.Dispose();
      Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromMilliseconds(500), $"Dispose took {stopwatch.Elapsed.TotalMilliseconds:n0} ms");
      Assert.IsTrue(cancelled.Wait(TimeSpan.FromSeconds(5)), "The save is cancelled");
      // No more saves after Dispose.
      scheduler.NotifyChanged(immediate: true);
      Assert.IsFalse(scheduler.IsDirty);
    }

    private static void WaitFor(Func<bool> condition, string description) {
      var stopwatch = Stopwatch.StartNew();
      while (!condition()) {
        Assert.IsTrue(stopwatch.Elapsed < TimeSpan.FromSeconds(10), $"Timeout waiting for: {description}");
        Thread.Sleep(10);
      }
    }
  }
}
