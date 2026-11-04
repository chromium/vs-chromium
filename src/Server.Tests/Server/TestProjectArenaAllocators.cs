// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Files;
using VsChromium.Core.Memory;
using VsChromium.Server.Diagnostics;
using VsChromium.Server.FileSystemContents;
using VsChromium.Server.FileSystemDatabase;
using VsChromium.Server.FileSystemNames;

namespace VsChromium.Tests.Server {
  [TestClass]
  public class TestProjectArenaAllocators {
    private static FileContents CreateContents(ArenaAllocator arenas, string text) {
      var bytes = Encoding.UTF8.GetBytes(text);
      var slice = arenas.Allocate(bytes.Length + 2);
      bytes.CopyTo(slice.Span);
      slice.Span.Slice(bytes.Length).Clear();
      return new Utf8FileContents(new FileContentsMemory(slice.Block, slice.Offset, bytes.Length), DateTime.UtcNow, isAscii: true);
    }

    [TestMethod]
    public void TheMemoryOfEachProjectIsItsOwn() {
      var allocators = new ProjectArenaAllocators(() => new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256));
      var factory = new FileSystemNameFactory();
      var rootA = factory.CreateAbsoluteDirectoryName(new FullPath(Path.Combine(Path.GetTempPath(), "a")));
      var rootB = factory.CreateAbsoluteDirectoryName(new FullPath(Path.Combine(Path.GetTempPath(), "b")));
      var subA = factory.CreateDirectoryName(rootA, "sub");
      var names = new[] {
        factory.CreateFileName(rootA, "1.cc"), factory.CreateFileName(subA, "2.cc"), factory.CreateFileName(rootB, "3.cc"),
        factory.CreateFileName(rootB, "large.cc"),
      };
      var contents = new[] {
        CreateContents(allocators.Get(rootA.FullPath), "text of 1"), // 16 bytes slot
        CreateContents(allocators.Get(rootA.FullPath), "text of the second file"), // 32 bytes slot
        CreateContents(allocators.Get(rootB.FullPath), "3"), // 8 bytes slot
        CreateContents(allocators.Get(rootB.FullPath), new string('x', 300)), // Dedicated block
      };
      // A dead slot in the arena of B.
      CreateContents(allocators.Get(rootB.FullPath), "edited");
      var database = new FileDatabaseSnapshot(new Dictionary<FullPath, string>(),
        new Dictionary<DirectoryName, FileRange> {
          [rootA] = new FileRange(0, 1), [subA] = new FileRange(1, 1), [rootB] = new FileRange(2, 2),
        },
        names, contents);

      var statistics = FileContentsStatistics.GetProjectStatistics(allocators, database);
      Assert.AreEqual(2, statistics.Count);
      var a = statistics[rootA.FullPath];
      var b = statistics[rootB.FullPath];
      Assert.AreEqual(1, a.ArenaCount);
      Assert.AreEqual(16 + 32, a.LiveBytes);
      Assert.AreEqual(0, a.DeadBytes);
      Assert.AreEqual(0, a.OtherBytes);
      Assert.AreEqual(1, b.ArenaCount);
      Assert.AreEqual(8, b.LiveBytes);
      Assert.AreEqual(8, b.DeadBytes, "\"edited\"");
      Assert.AreEqual(304, b.OtherBytes, "Large file");
      var sum = FileContentsStatistics.Sum(statistics.Values);
      Assert.AreEqual(2, sum.ArenaCount);
      Assert.AreEqual(16 + 32 + 8, sum.LiveBytes);
      Assert.AreEqual(2048, sum.ArenaBytes);
      GC.KeepAlive(contents);
    }

    [TestMethod]
    public void TheArenasOfARemovedProjectAreReleased() {
      var allocators = new ProjectArenaAllocators(() => new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256));
      var rootA = new FullPath(Path.Combine(Path.GetTempPath(), "a"));
      var rootB = new FullPath(Path.Combine(Path.GetTempPath(), "b"));
      var contentsA = CreateContents(allocators.Get(rootA), "text of a");
      var weakArenaB = AllocateInProject(allocators, rootB);
      AssertProjects(allocators, rootA, rootB);

      // Project B is removed: its arenas are released (no contents reference them anymore).
      allocators.Retain(new[] { rootA });
      Assert.IsFalse(allocators.TryGet(rootB, out _));
      for (var i = 0; i < 10 && IsAlive(weakArenaB); i++) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
      Assert.IsFalse(IsAlive(weakArenaB), "The arena of B is released");
      Assert.AreEqual(1, allocators.Get(rootA).GetArenas().Count);
      GC.KeepAlive(contentsA);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static bool IsAlive<T>(WeakReference<T> reference) where T : class {
      return reference.TryGetTarget(out _);
    }

    // Note: Not inlined: the allocator of B is not kept alive by the frame of the test (debug builds).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AssertProjects(ProjectArenaAllocators allocators, FullPath rootA, FullPath rootB) {
      Assert.AreSame(allocators.Get(rootA), allocators.Get(rootA));
      CollectionAssert.AreEquivalent(new[] { rootA, rootB }, allocators.GetAll().Select(x => x.Key).ToArray());
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<SafeNativeMemoryHandle> AllocateInProject(ProjectArenaAllocators allocators, FullPath root) {
      var contents = CreateContents(allocators.Get(root), "text of b");
      return new WeakReference<SafeNativeMemoryHandle>(allocators.Get(root).GetArenas().Single().Block);
    }
  }
}
