// Copyright 2026 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Memory;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestArenaAllocator {
    [TestMethod]
    public void SmallAllocationsShareArenas() {
      var allocator = new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256);
      var a = allocator.Allocate(10);
      var b = allocator.Allocate(20);

      Assert.AreSame(a.Block, b.Block);
      Assert.IsFalse(a.IsDedicated);
      Assert.AreEqual(10, a.Length);
      Assert.AreEqual(20, b.Length);
      // 8-byte aligned slots, in allocation order.
      Assert.AreEqual(0, a.Offset);
      Assert.AreEqual(16, b.Offset);
      Assert.AreEqual(1, CountArenas(allocator));
      Assert.AreEqual(16 + 24, allocator.GetArenas()[0].UsedBytes);

      // Releasing a slot of an arena doesn't release the arena.
      a.Release();
      Assert.IsFalse(b.Block.IsClosed);
    }

    [TestMethod]
    public void LargeAllocationsHaveDedicatedBlocks() {
      var allocator = new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256);
      var large = allocator.Allocate(257);
      Assert.IsTrue(large.IsDedicated);
      Assert.AreEqual(0, large.Offset);
      Assert.AreEqual(257, large.Length);
      Assert.AreEqual(0, allocator.GetArenas().Count);

      large.Release();
      Assert.IsTrue(large.Block.IsClosed);
    }

    [TestMethod]
    public void FullArenasAreReplaced() {
      var allocator = new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256);
      var slices = Enumerable.Range(0, 10).Select(_ => allocator.Allocate(200)).ToList();

      // 5 slots of 200 bytes per arena of 1024 bytes.
      Assert.AreEqual(2, slices.Select(x => x.Block).Distinct().Count());
      Assert.IsTrue(slices.All(x => x.Offset + x.Length <= 1024));
    }

    [TestMethod]
    public void ConcurrentAllocationsDontOverlap() {
      var allocator = new ArenaAllocator(arenaSize: 64 * 1024, largeAllocationSize: 4096);
      var slices = new ConcurrentBag<NativeMemorySlice>();
      Parallel.For(0, 20_000, new ParallelOptions { MaxDegreeOfParallelism = 16 }, i => {
        var slice = allocator.Allocate(1 + i % 600);
        slice.Span.Fill((byte)(i % 251));
        slices.Add(slice);
      });

      foreach (var group in slices.GroupBy(x => x.Block)) {
        var ordered = group.OrderBy(x => x.Offset).ToList();
        for (var i = 1; i < ordered.Count; i++) {
          Assert.IsTrue(ordered[i - 1].Offset + ordered[i - 1].Length <= ordered[i].Offset, "Overlapping slots");
        }
        Assert.IsTrue(ordered.Last().Offset + ordered.Last().Length <= 64 * 1024);
      }
    }

    [TestMethod]
    public void ArenasAreReleasedWhenTheirSlicesAreNotReferenced() {
      var allocator = new ArenaAllocator(arenaSize: 1024, largeAllocationSize: 256);
      AllocateAndDrop(allocator);
      // A new arena is current: the first one is only referenced by the dropped slices.
      var kept = allocator.Allocate(10);
      for (var i = 0; i < 5 && CountArenas(allocator) > 1; i++) {
        GC.Collect();
        GC.WaitForPendingFinalizers();
      }
      Assert.AreEqual(1, CountArenas(allocator));
      Assert.AreSame(kept.Block, allocator.GetArenas()[0].Block);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int CountArenas(ArenaAllocator allocator) {
      // Note: In a separate (not inlined) method, so that the list (strong references to the
      // blocks) is not kept alive by a stack slot of the caller during the collections.
      return allocator.GetArenas().Count;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AllocateAndDrop(ArenaAllocator allocator) {
      for (var i = 0; i < 5; i++) {
        allocator.Allocate(200);
      }
      // Fills the first arena: the next allocation uses a new arena.
      allocator.Allocate(200);
    }
  }
}
