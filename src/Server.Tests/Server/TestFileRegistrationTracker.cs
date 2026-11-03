// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestFileRegistrationTracker : ServerServicesTestBase {
    /// <summary>
    /// Regression test: VS Code registers its workspace folders (directories). Registered
    /// directories used to be considered deleted (file existence check) at the next registration
    /// (e.g. opening a document), which triggered a full re-index of the project each time.
    /// </summary>
    [TestMethod]
    public void RegisteredDirectoryIsNotConsideredDeleted() {
      using (var container = CreateServerServices()) {
        var tracker = container.FileRegistrationTracker;
        var changeCount = 0;
        var changed = new ManualResetEventSlim(false);
        tracker.ProjectListChanged += (sender, args) => {
          Interlocked.Increment(ref changeCount);
          changed.Set();
        };

        var file = Utils.GetChromiumTestEnlistmentFile();
        tracker.RegisterFileAsync(new FullPath(file.Directory.FullName));
        Assert.IsTrue(changed.Wait(TimeSpan.FromSeconds(30)), "Project list not changed");
        Assert.AreEqual(1, Volatile.Read(ref changeCount), "Registering the directory");

        // Same project: the list of projects must not change.
        tracker.RegisterFileAsync(new FullPath(file.FullName));
        Thread.Sleep(1000);
        Assert.AreEqual(1, Volatile.Read(ref changeCount), "Registering a file of the same project changed the project list");
      }
    }
  }
}
