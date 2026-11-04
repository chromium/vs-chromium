// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Server.Cache;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestIndexCacheCleanup {
    private static readonly DateTime Now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
    private string _root;

    [TestInitialize]
    public void Initialize() {
      _root = Path.Combine(Path.GetTempPath(), "VsChromiumIndexCacheCleanupTest-" + Guid.NewGuid().ToString("N"));
      Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Cleanup() {
      try {
        Directory.Delete(_root, recursive: true);
      }
      catch (IOException) {
        // Best effort
      }
    }

    /// <summary>A project cache directory last used at <paramref name="lastUsedUtc"/>, of about <paramref name="size"/> bytes.</summary>
    private string CreateCache(string name, DateTime lastUsedUtc, int size = 100) {
      var path = Path.Combine(_root, name);
      Directory.CreateDirectory(Path.Combine(path, "arenas"));
      File.WriteAllText(Path.Combine(path, "root.txt"), @"C:\project\" + name);
      File.WriteAllBytes(Path.Combine(path, "index.bin"), new byte[10]);
      File.WriteAllBytes(Path.Combine(path, "arenas", "000001.arena"), new byte[size]);
      File.WriteAllBytes(Path.Combine(path, "lock"), Array.Empty<byte>());
      foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories)) {
        File.SetLastWriteTimeUtc(file, lastUsedUtc);
      }
      Directory.SetLastWriteTimeUtc(Path.Combine(path, "arenas"), lastUsedUtc);
      Directory.SetLastWriteTimeUtc(path, lastUsedUtc);
      return path;
    }

    [TestMethod]
    public void CachesNotUsedFor30DaysAreDeleted() {
      var old = CreateCache("old", Now.AddDays(-40));
      var recent = CreateCache("recent", Now.AddDays(-10));
      var deleted = IndexCacheCleanup.Run(_root, Array.Empty<string>(), TimeSpan.FromDays(30), long.MaxValue, Now);
      CollectionAssert.AreEqual(new[] { old }, deleted);
      Assert.IsFalse(Directory.Exists(old));
      Assert.IsTrue(Directory.Exists(recent));
    }

    [TestMethod]
    public void TheLastSaveOrOpenIsTheLastUse() {
      var saved = CreateCache("saved", Now.AddDays(-40));
      File.SetLastWriteTimeUtc(Path.Combine(saved, "index.bin"), Now.AddDays(-1));
      var opened = CreateCache("opened", Now.AddDays(-40));
      File.SetLastWriteTimeUtc(Path.Combine(opened, "root.txt"), Now.AddDays(-1));
      Assert.AreEqual(0, IndexCacheCleanup.Run(_root, Array.Empty<string>(), TimeSpan.FromDays(30), long.MaxValue, Now).Count);
    }

    [TestMethod]
    public void CachesInUseAreNeverDeleted() {
      var openByThisServer = CreateCache("open", Now.AddDays(-40));
      var lockedByAnotherServer = CreateCache("locked", Now.AddDays(-40));
      using (new FileStream(Path.Combine(lockedByAnotherServer, "lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
        var deleted = IndexCacheCleanup.Run(_root, new[] { openByThisServer }, TimeSpan.FromDays(30), 0, Now);
        Assert.AreEqual(0, deleted.Count);
      }
      Assert.IsTrue(File.Exists(Path.Combine(lockedByAnotherServer, "index.bin")), "Nothing deleted in a locked cache");
      Assert.IsTrue(Directory.Exists(openByThisServer));
    }

    [TestMethod]
    public void LeastRecentlyUsedCachesAreDeletedAboveTheSizeLimit() {
      var a = CreateCache("a", Now.AddDays(-3), 1000);
      var b = CreateCache("b", Now.AddDays(-2), 1000);
      var c = CreateCache("c", Now.AddDays(-1), 1000);
      var size = new DirectoryInfo(_root).EnumerateFiles("*", SearchOption.AllDirectories).Sum(x => x.Length);
      // Room for 2 caches: the least recently used one is deleted.
      var deleted = IndexCacheCleanup.Run(_root, Array.Empty<string>(), TimeSpan.FromDays(30), size * 2 / 3 + 10, Now);
      CollectionAssert.AreEqual(new[] { a }, deleted);
      Assert.IsTrue(Directory.Exists(b) && Directory.Exists(c));
    }

    [TestMethod]
    public void SizeLimitSkipsCachesInUse() {
      var a = CreateCache("a", Now.AddDays(-3), 1000);
      var b = CreateCache("b", Now.AddDays(-2), 1000);
      CreateCache("c", Now.AddDays(-1), 1000);
      var deleted = IndexCacheCleanup.Run(_root, new[] { a }, TimeSpan.FromDays(30), 2500, Now);
      CollectionAssert.AreEqual(new[] { b }, deleted);
    }
  }
}
