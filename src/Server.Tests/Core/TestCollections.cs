// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromium.Core.Collections;
using VsChromium.Core.Linq;
using VsChromium.Core.Logging;
using VsChromium.Core.Utility;

namespace VsChromium.Tests.Core {
  [TestClass]
  public class TestCollections {
    [TestMethod]
    public void ConcurrentBitArrayWorks() {
      TestBitArray(new ConcurrentBitArray(10));
    }

    [TestMethod]
    public void PartitionedBitArrayWorks() {
      TestBitArray(new PartitionedBitArray(12000, 35));
    }

    private static void TestBitArray(IBitArray bits) {
      Assert.AreEqual(false, bits.Get(0));
      Assert.AreEqual(false, bits.Get(10000));
      Assert.AreEqual(0, bits.Count);

      bits.Set(100, false);
      Assert.AreEqual(false, bits.Get(100));
      Assert.AreEqual(0, bits.Count);

      bits.Set(100, true);
      Assert.AreEqual(true, bits.Get(100));
      Assert.AreEqual(1, bits.Count);

      bits.Set(100, false);
      Assert.AreEqual(false, bits.Get(100));
      Assert.AreEqual(0, bits.Count);

      bits.Set(63, true);
      Assert.AreEqual(true, bits.Get(63));
      Assert.AreEqual(1, bits.Count);
    }

    [TestMethod]
    public void HeapWorks() {
      var heap = new MaxHeap<int>();
      heap.Add(5);
      heap.Add(6);
      heap.Add(4);
      heap.Add(1);
      heap.Add(-1);

      Assert.AreEqual(6, heap.Remove());
      Assert.AreEqual(5, heap.Remove());
      Assert.AreEqual(4, heap.Remove());
      Assert.AreEqual(1, heap.Remove());
      Assert.AreEqual(-1, heap.Remove());
    }

    [TestMethod]
    public void PartitionEvenlyWorks() {
      var list = new List<int> {
        1,
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        13,
        14,
        15
      };

      Func<int, long> weight = i => i * 2;
      var partitions = list.PartitionWithWeight(weight, 3).ToList();

      Assert.AreEqual(3, partitions.Count);

      // Partition size are well-defined if the # of elements is not exactly a factor of the partition count.
      Assert.AreEqual(4, partitions[0].Count);
      Assert.AreEqual(4, partitions[1].Count);
      Assert.AreEqual(3, partitions[2].Count);

      // We don't care about what particular elements are inside the partitions,
      // but we do care that they are all the same weight
      Assert.AreEqual(26, partitions[0].Aggregate((x, y) => x + y));
      Assert.AreEqual(26, partitions[1].Aggregate((x, y) => x + y));
      Assert.AreEqual(26, partitions[2].Aggregate((x, y) => x + y));

      // All the elements of the initial list must be present in the union of the partitions.
      Assert.IsTrue(list.OrderBy(x => x).SequenceEqual(partitions.SelectMany(x => x).OrderBy(x => x)));
    }

    [TestMethod]
    public void PartitionByChunksWorks() {
      var list = new List<int> {
        1,
        2,
        3,
        4,
        5,
        6,
        7,
        8,
        13,
        14,
        15
      };

      var partitions = list.PartitionByChunks(3);

      Assert.AreEqual(3, partitions.Count);

      // Partition size are well-defined if the # of elements is not exactly a factor of the partition count.
      Assert.AreEqual(4, partitions[0].Count);
      Assert.AreEqual(4, partitions[1].Count);
      Assert.AreEqual(3, partitions[2].Count);

      // All the elements of the initial list must be present in the union of the partitions.
      Assert.IsTrue(list.OrderBy(x => x).SequenceEqual(partitions.SelectMany(x => x).OrderBy(x => x)));
    }

    [TestMethod]
    public void SmallArrayDiffsWorks() {
      var left = new[] { 5, 4, 6, 10 };
      var right = new[] { 10, 1 };
      var diffs = ArrayUtilities.BuildArrayDiffsForSmallArrays(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 3);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 1);
      Assert.AreEqual(diffs.CommonItems.Count, 1);
    }

    [TestMethod]
    public void LargeArrayDiffsWorks() {
      var left = new[] { 5, 4, 6, 10 };
      var right = new[] { 10, 1 };
      var diffs = ArrayUtilities.BuildArrayDiffsForLargeArrays(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 3);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 1);
      Assert.AreEqual(diffs.CommonItems.Count, 1);
    }

    [TestMethod]
    public void ArrayDiffShouldHandleEmptyLeft() {
      var left = new int[] { };
      var right = new[] { 10, 1 };
      var diffs = ArrayUtilities.BuildArrayDiffs(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 0);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 2);
      Assert.AreEqual(diffs.CommonItems.Count, 0);
    }

    [TestMethod]
    public void ArrayDiffShouldHandleEmptyRight() {
      var left = new[] { 10, 1 };
      var right = new int[] { };
      var diffs = ArrayUtilities.BuildArrayDiffs(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 2);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 0);
      Assert.AreEqual(diffs.CommonItems.Count, 0);
    }
    [TestMethod]
    public void ArrayDiffShouldHandleEmptyLeftAndRight() {
      var left = new int[] { };
      var right = new int[] { };
      var diffs = ArrayUtilities.BuildArrayDiffs(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 0);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 0);
      Assert.AreEqual(diffs.CommonItems.Count, 0);
    }
    [TestMethod]
    public void ArrayDiffShouldHandleOrderedIdenticalSequences() {
      var left = new int[] { 1, 2, 3 };
      var right = new int[] { 1, 2, 3 };
      var diffs = ArrayUtilities.BuildArrayDiffs(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 0);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 0);
      Assert.AreEqual(diffs.CommonItems.Count, 3);
    }
    [TestMethod]
    public void ArrayDiffShouldHandleUnorderedIdenticalSequences() {
      var left = new int[] { 3, 2, 1 };
      var right = new int[] { 1, 2, 3 };
      var diffs = ArrayUtilities.BuildArrayDiffs(left, right);
      Assert.AreEqual(diffs.LeftOnlyItems.Count, 0);
      Assert.AreEqual(diffs.RightOnlyItems.Count, 0);
      Assert.AreEqual(diffs.CommonItems.Count, 3);
    }

    [TestMethod]
    public void ListEnumeratorWorks() {
      //const int iterationCount = 3;
      //const int loopCount = 300000;
      const int iterationCount = 1;
      const int loopCount = 3000;
      Logger.IsDebugEnabled = true;
      Logger.IsInfoEnabled = false;

      var items = new List<int>();
      IList<int> itemsAsIList = items;
      var itemsArray = new int[100];
      for (var i = 0; i < 100; i++) {
        items.Add(i);
        itemsArray[i] = i;
      }

      for (var iteration = 0; iteration < iterationCount; iteration++) {
        Logger.LogDebug("Iteration #{0}", iteration);
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "for loop, List<T>:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            for (var i = 0; i < items.Count; i++) {
              var item = items[i];
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "ToEnum, List<T>:"))) {
          for (var i = 0; i < loopCount; i++) {
            foreach (var item in items.ToForeachEnum()) {
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "foreach, List<T>:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            foreach (var item in items) {
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "for loop, IList<T>:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            for (var i = 0; i < itemsAsIList.Count; i++) {
              var item = items[i];
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "ToEnum, IList<T>:"))) {
          for (var i = 0; i < loopCount; i++) {
            foreach (var item in itemsAsIList.ToForeachEnum()) {
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "foreach, IList<T>:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            foreach (var item in itemsAsIList) {
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "for loop, T[]:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            for (var i = 0; i < itemsArray.Length; i++) {
              var item = items[i];
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "ToEnum, T[]:"))) {
          for (var i = 0; i < loopCount; i++) {
            foreach (var item in itemsArray.ToForeachEnum()) {
            }
          }
        }
        GabargeCollect();
        using (new TimeElapsedLogger(string.Format("{0,-20}", "foreach, T[]:"))) {
          for (var loop = 0; loop < loopCount; loop++) {
            foreach (var item in itemsArray) {
            }
          }
        }
      }
    }

    private static void GabargeCollect() {
      GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
      GC.WaitForPendingFinalizers();
    }
  }
}
