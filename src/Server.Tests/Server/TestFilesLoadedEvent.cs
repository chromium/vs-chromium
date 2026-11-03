// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Ipc.TypedMessages;

namespace VsChromium.Tests.Server {
  /// <summary>
  /// Clients search as soon as they are notified that the files are loaded: the "FilesLoaded" event
  /// must be raised after the new file database is active. Regression test: the event used to be
  /// raised a first time before the activation (searches returned the results of the previous
  /// database), which made <see cref="TestSearchCode"/> flaky.
  /// </summary>
  [TestClass]
  public class TestFilesLoadedEvent : ServerServicesTestBase {
    [TestMethod]
    public void SearchesInFilesLoadedHandlersSeeTheNewDatabase() {
      using (var container = CreateServerServices()) {
        var searchEngine = container.SearchEngine;
        var hitCounts = new List<long>();
        var loaded = new ManualResetEventSlim(false);
        searchEngine.FilesLoaded += (sender, result) => {
          var searchResult = searchEngine.SearchCode(new SearchParams {
            SearchString = "Test directory looking like",
            MaxResults = 100,
            MatchCase = true,
          });
          lock (hitCounts) {
            hitCounts.Add(searchResult.HitCount);
          }
          loaded.Set();
        };

        container.FileRegistrationTracker.RegisterFileAsync(new FullPath(Utils.GetChromiumTestEnlistmentFile().FullName));
        Assert.IsTrue(loaded.Wait(TimeSpan.FromSeconds(30)), "Files not loaded");
        // Let other (wrong) events arrive, if any.
        Thread.Sleep(500);

        lock (hitCounts) {
          CollectionAssert.AreEqual(new long[] { 1 }, hitCounts, "Hits seen by each FilesLoaded handler call");
        }
      }
    }
  }
}
