// Copyright 2013 The Chromium Authors. All rights reserved.
// Use of this source code is governed by a BSD-style license that can be
// found in the LICENSE file.

using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VsChromiumCore.Collections;
using VsChromiumCore.Linq;

namespace VsChromiumTests {
  [TestClass]
  public class TestCollections {
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

      var partitions = list.PartitionEvenly(i => i * 2, 3).ToList();

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
  }
}
